using Order.Application.Abstractions;
using Order.Application.Orders;

namespace Order.API.Workers;

public sealed class OrderRecoveryWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<OrderRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                IReadOnlyList<Guid> ids;
                await using (var scope = scopes.CreateAsyncScope())
                    ids = await scope.ServiceProvider.GetRequiredService<IOrderProcessingStore>().GetDueIdsAsync(clock.GetUtcNow(), stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        await using var scope = scopes.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<IOrderProcessingService>().ProcessAsync(id, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (Exception exception) { logger.LogError(exception, "No se pudo recuperar la orden {OrderId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "No se pudieron consultar las órdenes pendientes"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
