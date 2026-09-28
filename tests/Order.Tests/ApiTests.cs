using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Order.Tests;

public sealed class ApiTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpointsAreAvailable(string path)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SwaggerDescribesFoundationWithoutPrematureBusinessEndpoints()
    {
        using var development = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();
        Assert.Contains("Swagger", await client.GetStringAsync("/"));
        var schema = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.Equal("Order API", schema.GetProperty("info").GetProperty("title").GetString());
        Assert.Empty(schema.GetProperty("paths").EnumerateObject());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/orders", new { })).StatusCode);
    }
}
