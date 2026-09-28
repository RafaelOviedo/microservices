using FluentValidation;

namespace Order.Application.Orders;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public const int MaximumItems = 100;

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("El ID del cliente es obligatorio.");
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("La orden debe contener al menos un ítem.")
            .Must(items => items!.Count <= MaximumItems).WithMessage("La orden admite hasta 100 productos.")
            .Must(items => items is null || items.Where(item => item is not null).Select(item => item!.ProductId).Distinct().Count()
                == items.Count(item => item is not null)).WithMessage("Cada producto debe aparecer una sola vez.");
        RuleForEach(x => x.Items).NotNull().WithMessage("Los ítems no pueden ser nulos.");
        RuleForEach(x => x.Items).SetValidator(new OrderItemRequestValidator()!);
    }
}

public sealed class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("El ID del producto es obligatorio.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero.");
    }
}
