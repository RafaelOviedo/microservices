using Order.Domain.Exceptions;
using Order.Domain.ValueObjects;

namespace Order.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];
    public Guid Id { get; private set; }
    public CustomerSnapshot Customer { get; private set; } = null!;
    public DateTimeOffset OrderedAtUtc { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.PendingStockConfirmation;
    public Money Total { get; private set; } = null!;
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? NextAttemptAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? FailureReason { get; private set; }
    public string? LastError { get; private set; }
    public Guid Version { get; private set; }

    private Order() { }

    public static Order Create(CustomerSnapshot customer, IEnumerable<OrderItem> items, DateTimeOffset now)
    {
        if (customer is null) throw new DomainValidationException("The customer is required.");
        if (items is null) throw new DomainValidationException("Items are required.");
        var lines = items.ToList();
        if (lines.Count == 0) throw new DomainValidationException("The order must contain at least one item.");
        if (lines.Any(item => item is null)) throw new DomainValidationException("Items cannot be null.");
        if (lines.Select(item => item.ProductId).Distinct().Count() != lines.Count)
            throw new DomainValidationException("Each product must appear in exactly one order line.");
        decimal total = 0;
        foreach (var item in lines) total = Money.From(total + item.Subtotal.Amount).Amount;
        var order = new Order
        {
            Id = Guid.NewGuid(), Customer = customer, OrderedAtUtc = NormalizeTimestamp(now),
            Total = Money.From(total), Version = Guid.NewGuid()
        };
        order._items.AddRange(lines.Select(item => item.CopyForOrder(order.Id)));
        return order;
    }

    public void BeginConfirmation(DateTimeOffset now)
    {
        if (Status != OrderStatus.PendingStockConfirmation) return;
        if (IsDeleted) throw new DomainValidationException("The order has been deleted.");
        Status = OrderStatus.Confirming;
        NextAttemptAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public void BeginCompensation(string reason, DateTimeOffset now)
    {
        if (Status == OrderStatus.Confirmed) throw new DomainValidationException("A confirmed order cannot be cancelled.");
        if (Status is OrderStatus.Cancelled or OrderStatus.CompensationPending) return;
        Status = OrderStatus.CompensationPending;
        FailureReason = reason;
        NextAttemptAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public void Confirm(IReadOnlyDictionary<Guid, int> quantities, DateTimeOffset now)
    {
        if (Status != OrderStatus.Confirming) throw new DomainValidationException("The order is not being confirmed.");
        if (quantities.Count != _items.Count || _items.Any(item => !quantities.TryGetValue(item.ProductId, out var quantity)
            || quantity < 0 || quantity > item.Quantity)) throw new DomainValidationException("Invalid stock allocation.");
        var total = Money.From(_items.Sum(item => item.UnitPrice.Amount * quantities[item.ProductId]));
        foreach (var item in _items) item.ConfirmQuantity(quantities[item.ProductId]);
        Total = total;
        Status = OrderStatus.Confirmed;
        ConfirmedAtUtc = NormalizeTimestamp(now);
        Finish();
    }

    public void Reject(string reason)
    {
        if (Status != OrderStatus.Confirming) throw new DomainValidationException("The order is not being confirmed.");
        Status = OrderStatus.Rejected;
        FailureReason = reason;
        Finish();
    }

    public void CompleteCompensation()
    {
        if (Status != OrderStatus.CompensationPending) throw new DomainValidationException("There is no pending compensation.");
        Status = OrderStatus.Cancelled;
        Finish();
    }

    public void ScheduleRetry(string error, DateTimeOffset now)
    {
        if (Status is not (OrderStatus.Confirming or OrderStatus.CompensationPending)) return;
        RetryCount = Math.Min(RetryCount + 1, 1000);
        LastError = error;
        NextAttemptAtUtc = NormalizeTimestamp(now.AddSeconds(Math.Min(60, Math.Pow(2, Math.Min(RetryCount, 6)))));
        Version = Guid.NewGuid();
    }

    private void Finish()
    {
        NextAttemptAtUtc = null;
        LastError = null;
        Version = Guid.NewGuid();
    }

    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted) return;
        if (Status is OrderStatus.Confirming or OrderStatus.CompensationPending)
            throw new DomainValidationException("The stock operation must finish before the order can be soft deleted.");
        IsDeleted = true;
        DeletedAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
