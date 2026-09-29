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
                    catch (Exception exception) { logger.LogError(exception, "Failed to recover order {OrderId}", id); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Failed to retrieve pending orders"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
