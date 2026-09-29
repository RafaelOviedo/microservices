using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Order.Application.Exceptions;
using Order.Domain.Exceptions;

namespace Order.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client cancelled the request; there is no response left to send.
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) throw;

            var problem = CreateProblem(exception);
            problem.Instance = context.Request.Path;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (problem.Status >= 500)
                logger.LogError(exception, "Unhandled error in {Method} {Path}; TraceId {TraceId}",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);
            else
                logger.LogWarning("Request rejected: {ErrorType}; TraceId {TraceId}",
                    exception.GetType().Name, context.TraceIdentifier);

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, problem.GetType(),
                JsonOptions, context.RequestAborted);
        }
    }

    private static ProblemDetails CreateProblem(Exception exception) => exception switch
    {
        ValidationException validation => new ValidationProblemDetails(validation.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The submitted data is invalid."
        },
        DomainValidationException => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest, Title = "Business rule violation.", Detail = exception.Message
        },
        OrderNotFoundException or ReferencedResourceNotFoundException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound, Title = "Resource not found.", Detail = exception.Message
        },
        UpstreamServiceException upstream => new ProblemDetails
        {
            Status = upstream.IsTimeout ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status502BadGateway,
            Title = "A required service could not be reached.", Detail = upstream.Message
        },
        OrderStateConflictException or ConcurrencyConflictException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict, Title = "Update conflict.", Detail = exception.Message
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal server error.",
            Detail = "The operation could not be completed. Use the traceId to identify the error."
        }
    };
}
