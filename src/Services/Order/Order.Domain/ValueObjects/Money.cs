using Order.Domain.Exceptions;

namespace Order.Domain.ValueObjects;

public sealed record Money
{
    public const decimal MaximumAmount = 9999999999999999.99m;
    public decimal Amount { get; }

    private Money(decimal amount) => Amount = amount;

    public static Money From(decimal amount)
    {
        if (amount <= 0 || amount > MaximumAmount)
            throw new DomainValidationException("The amount must be positive and must not exceed 9999999999999999.99.");
        if (decimal.Round(amount, 2) != amount)
            throw new DomainValidationException("The amount must have at most two decimal places.");

        return new Money(amount);
    }
}
