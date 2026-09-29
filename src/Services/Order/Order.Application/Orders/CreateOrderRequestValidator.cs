using FluentValidation;

namespace Order.Application.Orders;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public const int MaximumItems = 100;

    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("The customer ID is required.");
        RuleFor(x => x.Items).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("The order must contain at least one item.")
            .Must(items => items!.Count <= MaximumItems).WithMessage("The order can contain at most 100 products.")
            .Must(items => items is null || items.Where(item => item is not null).Select(item => item!.ProductId).Distinct().Count()
                == items.Count(item => item is not null)).WithMessage("Each product must appear only once.");
        RuleForEach(x => x.Items).NotNull().WithMessage("Items cannot be null.");
        RuleForEach(x => x.Items).SetValidator(new OrderItemRequestValidator()!);
    }
}

public sealed class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("The product ID is required.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("The quantity must be greater than zero.");
    }
}
