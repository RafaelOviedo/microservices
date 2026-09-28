using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Customer.Application.Exceptions;
using Customer.Domain.Exceptions;

namespace Customer.API.Middleware;

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
            // El cliente canceló la solicitud; ya no hay una respuesta que enviar.
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) throw;

            var problem = CreateProblem(exception);
            problem.Instance = context.Request.Path;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (problem.Status >= 500)
                logger.LogError(exception, "Error no controlado en {Method} {Path}; TraceId {TraceId}",
                    context.Request.Method, context.Request.Path, context.TraceIdentifier);
            else
                logger.LogWarning("Solicitud rechazada: {ErrorType}; TraceId {TraceId}",
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
            Title = "Los datos enviados no son válidos."
        },
        DomainValidationException => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest, Title = "Regla de negocio inválida.", Detail = exception.Message
        },
        CustomerNotFoundException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound, Title = "Cliente no encontrado.", Detail = exception.Message
        },
        EmailAlreadyUsedException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict, Title = "Email en uso.", Detail = exception.Message
        },
        ConcurrencyConflictException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict, Title = "Conflicto de actualización.", Detail = exception.Message
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno del servidor.",
            Detail = "No se pudo completar la operación. Usá el traceId para identificar el error."
        }
    };
}
