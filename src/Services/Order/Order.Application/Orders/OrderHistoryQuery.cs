using Order.Domain.Orders;

namespace Order.Application.Orders;

public sealed class OrderHistoryQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? CustomerId { get; init; }
    public OrderStatus? Status { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record OrderHistoryResponse(IReadOnlyList<OrderResponse> Items, int Page, int PageSize, int TotalCount);
