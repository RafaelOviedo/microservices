namespace Product.Domain.Stock;

public enum StockOperationStatus { Applied, Rejected, Cancelled }
public sealed record StockAllocation(Guid ProductId, int RequestedQuantity, int AcceptedQuantity);

public sealed class StockOperation
{
    public Guid Id { get; private set; }
    public string? RequestHash { get; private set; }
    public StockOperationStatus Status { get; private set; }
    public IReadOnlyList<StockAllocation> Allocations { get; private set; } = Array.Empty<StockAllocation>();
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    private StockOperation() { }

    public static StockOperation Applied(Guid id, string hash, IEnumerable<StockAllocation> allocations, DateTimeOffset now)
        => new() { Id = id, RequestHash = hash, Status = StockOperationStatus.Applied,
            Allocations = allocations.ToArray(), CreatedAtUtc = now.ToUniversalTime() };
    public static StockOperation Rejected(Guid id, string hash, string reason, DateTimeOffset now)
        => new() { Id = id, RequestHash = hash, Status = StockOperationStatus.Rejected, Reason = reason, CreatedAtUtc = now.ToUniversalTime() };
    public static StockOperation Cancelled(Guid id, DateTimeOffset now)
        => new() { Id = id, Status = StockOperationStatus.Cancelled, CreatedAtUtc = now.ToUniversalTime() };
    public void Cancel() => Status = StockOperationStatus.Cancelled;
}
