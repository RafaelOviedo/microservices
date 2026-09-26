using FluentValidation;
using Product.Domain.ValueObjects;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Application.Products;

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(ProductEntity.NameMaxLength).WithMessage("El nombre admite hasta 200 caracteres.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción es obligatoria.")
            .MaximumLength(ProductEntity.DescriptionMaxLength).WithMessage("La descripción admite hasta 2000 caracteres.");
        RuleFor(x => x.Price)
            .NotNull().WithMessage("El precio es obligatorio.")
            .GreaterThan(0m).WithMessage("El precio debe ser mayor que cero.")
            .LessThanOrEqualTo(Money.MaximumAmount).WithMessage("El precio excede el importe máximo permitido.")
            .Must(value => value is null || decimal.Round(value.Value, 2) == value.Value)
                .WithMessage("El precio admite hasta dos decimales.");
        RuleFor(x => x.Stock)
            .NotNull().WithMessage("El stock es obligatorio.")
            .GreaterThanOrEqualTo(0).WithMessage("El stock no puede ser negativo.");
    }
}
