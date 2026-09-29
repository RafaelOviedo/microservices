using AutoMapper;
using FluentValidation;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Domain.Exceptions;
using Order.Domain.Orders;
using Order.Domain.ValueObjects;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Application.Orders;

public sealed class OrderService(IOrderRepository repository, ICustomerClient customers, IProductClient products,
    IValidator<CreateOrderRequest> validator, IValidator<OrderHistoryQuery> historyValidator, IMapper mapper, TimeProvider clock) : IOrderService
{
    public async Task<CreateOrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var customer = await customers.GetByIdAsync(request.CustomerId, cancellationToken)
            ?? throw new ReferencedResourceNotFoundException("customer", request.CustomerId);
        var lines = new List<OrderItem>();
        var adjustments = new List<QuantityAdjustment>();
        foreach (var requested in request.Items!)
        {
            var item = requested!;
            var product = await products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new ReferencedResourceNotFoundException("product", item.ProductId);
            var accepted = Math.Min(item.Quantity, product.Stock);
            if (accepted != item.Quantity) adjustments.Add(new(product.Id, item.Quantity, accepted));
            if (accepted > 0) lines.Add(OrderItem.Create(product.Id, product.Name, Money.From(product.Price), accepted));
        }
        if (lines.Count == 0) throw new DomainValidationException("None of the requested products are in stock.");
        var order = OrderEntity.Create(CustomerSnapshot.From(customer.Id, customer.Name), lines, clock.GetUtcNow());
        // Explicit confirmation deducts stock; creation leaves the order pending.
        repository.Add(order);
        await repository.SaveChangesAsync(cancellationToken);
        return new CreateOrderResponse(mapper.Map<OrderResponse>(order), adjustments);
    }

    public async Task<OrderHistoryResponse> ListAsync(OrderHistoryQuery query, CancellationToken cancellationToken)
    {
        await historyValidator.ValidateAndThrowAsync(query, cancellationToken);
        var (items, totalCount) = await repository.ListAsync(query, cancellationToken);
        return new OrderHistoryResponse(mapper.Map<OrderResponse[]>(items), query.Page, query.PageSize, totalCount);
    }

    public async Task<OrderResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(id, cancellationToken) ?? throw new OrderNotFoundException(id);
        return mapper.Map<OrderResponse>(order);
    }
}
