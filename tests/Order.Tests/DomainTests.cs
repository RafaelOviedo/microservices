using Order.Domain.Exceptions;
using Order.Domain.Orders;
using Order.Domain.ValueObjects;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Tests;

public sealed class DomainTests
{
    private static CustomerSnapshot Customer() => CustomerSnapshot.From(Guid.NewGuid(), " Ana Pérez ");
    private static OrderItem Item(decimal price = 12.34m, int quantity = 2)
        => OrderItem.Create(Guid.NewGuid(), " Teclado ", Money.From(price), quantity);

    [Fact]
    public void CreationCalculatesTotalsPreservesSnapshotsAndNormalizesDate()
    {
        var customer = Customer();
        var line = Item();
        var input = new List<OrderItem> { line, Item(0.01m, 3) };
        var now = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.FromHours(-3)).AddTicks(17);
        var order = OrderEntity.Create(customer, input, now);
        input.Clear();
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(24.71m, order.Total.Amount);
        Assert.Equal(24.68m, order.Items.First().Subtotal.Amount);
        Assert.Equal(customer.Id, order.Customer.Id);
        Assert.Equal("Ana Pérez", order.Customer.Name);
        Assert.Equal("Teclado", order.Items.First().ProductName);
        Assert.All(order.Items, item => Assert.Equal(order.Id, item.OrderId));
        Assert.NotSame(line, order.Items.First());
        Assert.Equal(TimeSpan.Zero, order.OrderedAtUtc.Offset);
        Assert.Equal(now.ToUniversalTime().AddTicks(-7), order.OrderedAtUtc);
        Assert.Throws<NotSupportedException>(() => ((ICollection<OrderItem>)order.Items).Clear());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    public void InvalidMoneyIsRejected(string amount)
        => Assert.Throws<DomainValidationException>(() => Money.From(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuantityMustBePositive(int quantity)
        => Assert.Throws<DomainValidationException>(() => Item(quantity: quantity));

    [Fact]
    public void SubtotalAndTotalCannotExceedStoragePrecision()
    {
        Assert.Throws<DomainValidationException>(() => Item(Money.MaximumAmount, 2));
        Assert.Throws<DomainValidationException>(() => OrderEntity.Create(Customer(), [Item(Money.MaximumAmount, 1), Item(0.01m, 1)], DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SnapshotNamesAreRequired(string name)
    {
        Assert.Throws<DomainValidationException>(() => CustomerSnapshot.From(Guid.NewGuid(), name));
        Assert.Throws<DomainValidationException>(() => OrderItem.Create(Guid.NewGuid(), name, Money.From(1), 1));
    }

    [Fact]
    public void SnapshotsRequireIdsAndBoundedNames()
    {
        Assert.Throws<DomainValidationException>(() => CustomerSnapshot.From(Guid.Empty, "Ana"));
        Assert.Throws<DomainValidationException>(() => OrderItem.Create(Guid.Empty, "Teclado", Money.From(1), 1));
        Assert.Throws<DomainValidationException>(() => CustomerSnapshot.From(Guid.NewGuid(), new string('a', 201)));
        Assert.Throws<DomainValidationException>(() => OrderItem.Create(Guid.NewGuid(), new string('a', 201), Money.From(1), 1));
        Assert.Throws<DomainValidationException>(() => OrderItem.Create(Guid.NewGuid(), "Teclado", null!, 1));
    }

    [Fact]
    public void OrderRequiresCustomerAndNonemptyDistinctItems()
    {
        Assert.Throws<DomainValidationException>(() => OrderEntity.Create(null!, [Item()], DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => OrderEntity.Create(Customer(), null!, DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => OrderEntity.Create(Customer(), [], DateTimeOffset.UtcNow));
        Assert.Throws<DomainValidationException>(() => OrderEntity.Create(Customer(), [null!], DateTimeOffset.UtcNow));
        var item = Item();
        Assert.Throws<DomainValidationException>(() => OrderEntity.Create(Customer(), [item, item], DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DeleteIsIdempotentAndPreservesHistoricalValues()
    {
        var order = OrderEntity.Create(Customer(), [Item()], DateTimeOffset.UtcNow);
        var orderedAt = order.OrderedAtUtc;
        var version = order.Version;
        var now = DateTimeOffset.UtcNow;
        order.Delete(now);
        Assert.True(order.IsDeleted);
        Assert.NotEqual(version, order.Version);
        Assert.Equal(0, order.DeletedAtUtc!.Value.Ticks % TimeSpan.TicksPerMicrosecond);
        var deletionVersion = order.Version;
        var deletedAt = order.DeletedAtUtc;
        order.Delete(now.AddDays(1));
        Assert.Equal(deletionVersion, order.Version);
        Assert.Equal(deletedAt, order.DeletedAtUtc);
        Assert.Equal(orderedAt, order.OrderedAtUtc);
        Assert.Equal(24.68m, order.Total.Amount);
        Assert.Single(order.Items);
    }
}
