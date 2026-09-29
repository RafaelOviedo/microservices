namespace Product.Application.Stock;

public sealed record StockRequest(IReadOnlyList<StockLineRequest?>? Items);
public sealed record StockLineRequest(Guid ProductId, int Quantity, decimal ExpectedUnitPrice);
public sealed record StockAllocationResponse(Guid ProductId, int RequestedQuantity, int AcceptedQuantity);
public sealed record StockOperationResponse(Guid Id, string Status, string? Reason, IReadOnlyList<StockAllocationResponse> Allocations);

public interface IStockOperationService
{
    Task<StockOperationResponse> ApplyAsync(Guid id, StockRequest request, CancellationToken cancellationToken);
    Task<StockOperationResponse> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<StockOperationResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
}
