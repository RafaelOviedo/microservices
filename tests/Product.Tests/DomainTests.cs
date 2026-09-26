using System.Globalization;
using Product.Domain.Exceptions;
using Product.Domain.ValueObjects;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Tests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10000000000000000")]
    public void MoneyRejectsInvalidAmounts(string amount)
        => Assert.Throws<DomainValidationException>(() => Money.From(decimal.Parse(amount, CultureInfo.InvariantCulture)));

    [Fact]
    public void MoneyUsesValueEqualityAndPreservesCents()
    {
        Assert.Equal(Money.From(12.34m), Money.From(12.34m));
        Assert.NotEqual(Money.From(12.34m), Money.From(12.35m));
    }

    [Fact]
    public void CreationNormalizesTextAndDate()
    {
        var product = ProductEntity.Create("  Teclado  ", "  Mecánico  ", Money.From(12.34m), 0, Now.ToOffset(TimeSpan.FromHours(-3)));
        Assert.Equal("Teclado", product.Name);
        Assert.Equal("Mecánico", product.Description);
        Assert.Equal(TimeSpan.Zero, product.CreatedAtUtc.Offset);
        Assert.Equal(Now, product.CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, product.Id);
    }

    [Theory]
    [InlineData(" ", "Descripción", 1)]
    [InlineData("Producto", " ", 1)]
    [InlineData("Producto", "Descripción", -1)]
    public void DomainGuardsRulesWithoutTheApi(string name, string description, int stock)
        => Assert.Throws<DomainValidationException>(() => ProductEntity.Create(name, description, Money.From(1), stock, Now));

    [Fact]
    public void FailedUpdateDoesNotPartiallyChangeTheAggregate()
    {
        var product = ProductEntity.Create("Original", "Descripción", Money.From(10), 4, Now);
        var version = product.Version;
        Assert.Throws<DomainValidationException>(() => product.Update("Nuevo", "Otra", Money.From(20), -1, Now.AddDays(1)));
        Assert.Equal("Original", product.Name);
        Assert.Equal(10, product.Price.Amount);
        Assert.Equal(4, product.Stock);
        Assert.Equal(version, product.Version);
        Assert.Null(product.UpdatedAtUtc);
    }

    [Fact]
    public void DeletedProductCannotBeEditedAndDeletionDateIsStable()
    {
        var product = ProductEntity.Create("Producto", "Descripción", Money.From(10), 4, Now);
        product.Delete(Now.AddDays(1));
        var version = product.Version;
        product.Delete(Now.AddDays(2));
        Assert.True(product.IsDeleted);
        Assert.Equal(Now.AddDays(1), product.DeletedAtUtc);
        Assert.Equal(version, product.Version);
        Assert.Throws<DomainValidationException>(() => product.Update("Nuevo", "Otra", Money.From(20), 5, Now.AddDays(2)));
    }
}
