using Microsoft.AspNetCore.Mvc;
using Product.Application.Stock;

namespace Product.API.Controllers;

[ApiController]
[Route("api/stock-operations")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
public sealed class StockOperationsController(IStockOperationService service) : ControllerBase
{
    [HttpPut("{id:guid}")]
    [ProducesResponseType<StockOperationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockOperationResponse>> Apply(Guid id, StockRequest request, CancellationToken cancellationToken)
        => Ok(await service.ApplyAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<StockOperationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockOperationResponse>> Cancel(Guid id, CancellationToken cancellationToken)
        => Ok(await service.CancelAsync(id, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<StockOperationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockOperationResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var operation = await service.GetAsync(id, cancellationToken);
        return operation is null ? NotFound() : Ok(operation);
    }
}
