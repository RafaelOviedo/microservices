using Microsoft.Extensions.Diagnostics.HealthChecks;
using Product.Infrastructure.Persistence;

namespace Product.API.Health;

public sealed class DatabaseHealthCheck(ProductDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("PostgreSQL no está disponible.");
    }
}
