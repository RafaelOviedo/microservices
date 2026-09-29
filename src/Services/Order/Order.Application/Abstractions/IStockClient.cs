namespace Order.Application.Abstractions;

public sealed record StockLine(Guid ProductId, int Quantity, decimal ExpectedUnitPrice);
public sealed record StockAllocation(Guid ProductId, int RequestedQuantity, int AcceptedQuantity);
public sealed record StockResult(Guid Id, string Status, string? Reason, IReadOnlyList<StockAllocation> Allocations);
public interface IStockClient
{
    Task<StockResult> ApplyAsync(Guid operationId, IReadOnlyList<StockLine> items, CancellationToken cancellationToken);
    Task<StockResult> CancelAsync(Guid operationId, CancellationToken cancellationToken);
}
