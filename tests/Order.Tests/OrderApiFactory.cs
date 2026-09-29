using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Order.Tests;

public sealed class OrderApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Las pruebas de integración requieren PostgreSQL de pruebas. Usá compose.order.tests.yaml.");
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderDb"] = connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Recovery:Enabled"] = "false",
                ["Logging:FilePath"] = Path.Combine(Path.GetTempPath(), "order-tests", "order-.log")
            }));
    }
}
