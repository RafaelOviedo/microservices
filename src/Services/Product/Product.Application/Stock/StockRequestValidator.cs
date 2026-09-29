using FluentValidation;
using Product.Domain.ValueObjects;

namespace Product.Application.Stock;

public sealed class StockRequestValidator : AbstractValidator<StockRequest>
{
    public StockRequestValidator()
    {
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(items => items!.Count <= 100)
            .Must(items => items is null || items.Where(x => x is not null).Select(x => x!.ProductId).Distinct().Count() == items.Count(x => x is not null));
        RuleForEach(x => x.Items).NotNull();
        RuleForEach(x => x.Items).SetValidator(new StockLineValidator()!);
    }
}
public sealed class StockLineValidator : AbstractValidator<StockLineRequest>
{
    public StockLineValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ExpectedUnitPrice).GreaterThan(0).LessThanOrEqualTo(Money.MaximumAmount)
            .Must(price => decimal.Round(price, 2) == price);
    }
}
