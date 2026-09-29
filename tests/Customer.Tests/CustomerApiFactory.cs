using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Customer.Tests;

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("CUSTOMER_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Integration tests require a test PostgreSQL instance. Use compose.customer.tests.yaml.");
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CustomerDb"] = connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Logging:FilePath"] = Path.Combine(Path.GetTempPath(), "customer-tests", "customer-.log")
            }));
    }
}
