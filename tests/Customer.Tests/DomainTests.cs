using Customer.Domain.Exceptions;
using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Tests;

public sealed class DomainTests
{
    private static Address Address() => Customer.Domain.ValueObjects.Address.From(" Calle 1 ", "Rosario", "Santa Fe", "2000", "Argentina");

    [Fact]
    public void ValueObjectsTrimAndCompareByValue()
    {
        Assert.Equal(Email.From(" Ana@Example.com "), Email.From("ana@example.com"));
        Assert.Equal(Address(), Address());
        Assert.Equal("Calle 1", Address().Street);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid")]
    [InlineData("ana@@example.com")]
    [InlineData("Ana <ana@example.com>")]
    [InlineData("ana @example.com")]
    public void InvalidEmailsAreRejected(string? value)
        => Assert.Throws<DomainValidationException>(() => Email.From(value!));

    [Fact]
    public void ExcessiveEmailLengthIsRejected()
        => Assert.Throws<DomainValidationException>(() => Email.From(new string('a', 250) + "@example.com"));

    [Theory]
    [InlineData("", "City", "State", "1234", "Country")]
    [InlineData("Street", "", "State", "1234", "Country")]
    [InlineData("Street", "City", "", "1234", "Country")]
    [InlineData("Street", "City", "State", "", "Country")]
    [InlineData("Street", "City", "State", "1234", "")]
    public void AddressRequiresEveryField(string street, string city, string state, string postalCode, string country)
        => Assert.Throws<DomainValidationException>(() => Customer.Domain.ValueObjects.Address.From(street, city, state, postalCode, country));

    [Fact]
    public void CustomerNormalizesDatesAndRejectsPartialInvalidUpdates()
    {
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.FromHours(-3)).AddTicks(17);
        var customer = CustomerEntity.Create(" Ana ", Email.From("ana@example.com"), Address(), now);
        Assert.Equal("Ana", customer.Name);
        Assert.Equal(TimeSpan.Zero, customer.RegisteredAtUtc.Offset);
        Assert.Equal(0, customer.RegisteredAtUtc.Ticks % TimeSpan.TicksPerMicrosecond);
        var version = customer.Version;
        Assert.Throws<DomainValidationException>(() => customer.Update("", Email.From("other@example.com"), Address(), now));
        Assert.Equal("ana@example.com", customer.Email.Value);
        Assert.Equal(version, customer.Version);
        customer.Delete(now);
        var deletionVersion = customer.Version;
        customer.Delete(now.AddDays(1));
        Assert.Equal(deletionVersion, customer.Version);
        Assert.Equal(customer.DeletedAtUtc, customer.UpdatedAtUtc);
        Assert.Throws<DomainValidationException>(() => customer.Update("Ana", customer.Email, Address(), now));
    }
}
