using FluentValidation;

namespace Order.Application.Orders;

public sealed class OrderHistoryQueryValidator : AbstractValidator<OrderHistoryQuery>
{
    public OrderHistoryQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.CustomerId).NotEqual(Guid.Empty).When(query => query.CustomerId.HasValue);
        RuleFor(query => query.Status).IsInEnum().When(query => query.Status.HasValue);
        RuleFor(query => query.To).Must((query, to) => !query.From.HasValue || !to.HasValue || to >= query.From)
            .WithMessage("The end date must be on or after the start date.");
        RuleFor(query => query.Page).Must((query, page) => ((long)page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page exceeds the supported range.");
    }
}
