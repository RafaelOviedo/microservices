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

    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted) return;
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
