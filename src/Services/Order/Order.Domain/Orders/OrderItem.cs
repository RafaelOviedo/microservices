using Order.Domain.Exceptions;
using Order.Domain.ValueObjects;

namespace Order.Domain.Orders;

public sealed class OrderItem
{
    public const int ProductNameMaxLength = 200;
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }
    public int? ConfirmedQuantity { get; private set; }
    public Money Subtotal { get; private set; } = null!;

    private OrderItem() { }

    public static OrderItem Create(Guid productId, string productName, Money unitPrice, int quantity)
    {
        if (productId == Guid.Empty) throw new DomainValidationException("El ID del producto es obligatorio.");
        if (string.IsNullOrWhiteSpace(productName) || productName.Length > ProductNameMaxLength)
            throw new DomainValidationException("El nombre del producto es obligatorio y admite hasta 200 caracteres.");
        if (unitPrice is null) throw new DomainValidationException("El precio unitario es obligatorio.");
        if (quantity <= 0) throw new DomainValidationException("La cantidad debe ser mayor que cero.");
        return new OrderItem
        {
            Id = Guid.NewGuid(), ProductId = productId, ProductName = productName.Trim(),
            UnitPrice = unitPrice, Quantity = quantity, Subtotal = Money.From(unitPrice.Amount * quantity)
        };
    }

    internal void ConfirmQuantity(int quantity)
    {
        if (quantity < 0 || quantity > Quantity) throw new DomainValidationException("Cantidad confirmada inválida.");
        ConfirmedQuantity = quantity;
    }

    // Cada agregado conserva instancias propias; una línea de entrada no se comparte entre órdenes.
    internal OrderItem CopyForOrder(Guid orderId)
    {
        var copy = Create(ProductId, ProductName, UnitPrice, Quantity);
        copy.OrderId = orderId;
        return copy;
    }
}
