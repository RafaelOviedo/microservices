using Microsoft.Extensions.Diagnostics.HealthChecks;
using Order.Infrastructure.Persistence;

namespace Order.API.Health;

public sealed class DatabaseHealthCheck(OrderDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
    }
}
