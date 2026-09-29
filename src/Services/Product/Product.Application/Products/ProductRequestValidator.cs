using FluentValidation;
using Product.Domain.ValueObjects;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Application.Products;

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("The name is required.")
            .MaximumLength(ProductEntity.NameMaxLength).WithMessage("The name must not exceed 200 characters.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("The description is required.")
            .MaximumLength(ProductEntity.DescriptionMaxLength).WithMessage("The description must not exceed 2000 characters.");
        RuleFor(x => x.Price)
            .NotNull().WithMessage("The price is required.")
            .GreaterThan(0m).WithMessage("The price must be greater than zero.")
            .LessThanOrEqualTo(Money.MaximumAmount).WithMessage("The price exceeds the maximum allowed amount.")
            .Must(value => value is null || decimal.Round(value.Value, 2) == value.Value)
                .WithMessage("The price must have at most two decimal places.");
        RuleFor(x => x.Stock)
            .NotNull().WithMessage("Stock is required.")
            .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");
    }
}
