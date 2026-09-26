using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Product.Tests;

public sealed class ProductApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("PRODUCT_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Las pruebas de integración requieren PostgreSQL de pruebas. Usá compose.tests.yaml.");
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ProductDb"] = connectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Logging:FilePath"] = Path.Combine(Path.GetTempPath(), "product-tests", "product-.log")
            }));
    }
}
