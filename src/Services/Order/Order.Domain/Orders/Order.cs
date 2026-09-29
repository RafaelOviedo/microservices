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
        if (customer is null) throw new DomainValidationException("El cliente es obligatorio.");
        if (items is null) throw new DomainValidationException("Los ítems son obligatorios.");
        var lines = items.ToList();
        if (lines.Count == 0) throw new DomainValidationException("La orden debe contener al menos un ítem.");
        if (lines.Any(item => item is null)) throw new DomainValidationException("Los ítems no pueden ser nulos.");
        if (lines.Select(item => item.ProductId).Distinct().Count() != lines.Count)
            throw new DomainValidationException("Cada producto debe aparecer en una única línea de la orden.");
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
        if (IsDeleted) throw new DomainValidationException("La orden está eliminada.");
        Status = OrderStatus.Confirming;
        NextAttemptAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public void BeginCompensation(string reason, DateTimeOffset now)
    {
        if (Status == OrderStatus.Confirmed) throw new DomainValidationException("No se puede cancelar una orden confirmada.");
        if (Status is OrderStatus.Cancelled or OrderStatus.CompensationPending) return;
        Status = OrderStatus.CompensationPending;
        FailureReason = reason;
        NextAttemptAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public void Confirm(IReadOnlyDictionary<Guid, int> quantities, DateTimeOffset now)
    {
        if (Status != OrderStatus.Confirming) throw new DomainValidationException("La orden no está en confirmación.");
        if (quantities.Count != _items.Count || _items.Any(item => !quantities.TryGetValue(item.ProductId, out var quantity)
            || quantity < 0 || quantity > item.Quantity)) throw new DomainValidationException("Asignación de stock inválida.");
        var total = Money.From(_items.Sum(item => item.UnitPrice.Amount * quantities[item.ProductId]));
        foreach (var item in _items) item.ConfirmQuantity(quantities[item.ProductId]);
        Total = total;
        Status = OrderStatus.Confirmed;
        ConfirmedAtUtc = NormalizeTimestamp(now);
        Finish();
    }

    public void Reject(string reason)
    {
        if (Status != OrderStatus.Confirming) throw new DomainValidationException("La orden no está en confirmación.");
        Status = OrderStatus.Rejected;
        FailureReason = reason;
        Finish();
    }

    public void CompleteCompensation()
    {
        if (Status != OrderStatus.CompensationPending) throw new DomainValidationException("No hay compensación pendiente.");
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
            throw new DomainValidationException("La operación de stock debe finalizar antes de dar de baja la orden.");
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
