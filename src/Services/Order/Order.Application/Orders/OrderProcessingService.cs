using AutoMapper;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Domain.Exceptions;
using Order.Domain.Orders;

namespace Order.Application.Orders;

public interface IOrderProcessingService
{
    Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken cancellationToken);
    Task<OrderResponse> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task<OrderResponse> ProcessAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class OrderProcessingService(IOrderProcessingStore store, IStockClient stock, ICustomerClient customers,
    IMapper mapper, TimeProvider clock) : IOrderProcessingService
{
    public async Task<OrderResponse> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        // Persist the intent BEFORE the first remote side effect to allow recovery after a restart.
        await store.ExecuteAsync(id, (order, _) => { order.BeginConfirmation(clock.GetUtcNow()); return Task.CompletedTask; }, cancellationToken);
        return await ProcessAsync(id, cancellationToken);
    }

    public async Task<OrderResponse> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await store.ExecuteAsync(id, (order, _) =>
        {
            if (order.Status == OrderStatus.Confirmed) throw new OrderStateConflictException();
            order.BeginCompensation("CancelledByUser", clock.GetUtcNow());
            return Task.CompletedTask;
        }, cancellationToken);
        return await ProcessAsync(id, cancellationToken);
    }

    public async Task<OrderResponse> ProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await store.ExecuteAsync(id, async (order, token) =>
        {
            try
            {
                if (order.Status == OrderStatus.Confirming)
                {
                    if (await customers.GetByIdAsync(order.Customer.Id, token) is null)
                    {
                        order.BeginCompensation("CustomerUnavailable", clock.GetUtcNow());
                        return; // Commit this local state before restoring stock.
                    }
                    var lines = order.Items.Select(item => new StockLine(item.ProductId, item.Quantity, item.UnitPrice.Amount)).ToArray();
                    var applied = await stock.ApplyAsync(order.Id, lines, token);
                    switch (applied.Status)
                    {
                        case "Applied":
                            try { order.Confirm(applied.Allocations.ToDictionary(x => x.ProductId, x => x.AcceptedQuantity), clock.GetUtcNow()); }
                            catch (DomainValidationException) { order.BeginCompensation("InvalidStockAllocation", clock.GetUtcNow()); }
                            break;
                        case "Rejected": order.Reject(applied.Reason ?? "StockRejected"); break;
                        case "Cancelled":
                            order.BeginCompensation("StockOperationCancelled", clock.GetUtcNow());
                            order.CompleteCompensation();
                            break;
                    }
                }
                else if (order.Status == OrderStatus.CompensationPending)
                {
                    await stock.CancelAsync(order.Id, token);
                    order.CompleteCompensation();
                }
            }
            catch (UpstreamServiceException exception)
            {
                // A timeout may occur after the remote commit. Retrying with the same ID is safe.
                order.ScheduleRetry(exception.Message, clock.GetUtcNow());
            }
        }, cancellationToken);
        return mapper.Map<OrderResponse>(result);
    }
}
