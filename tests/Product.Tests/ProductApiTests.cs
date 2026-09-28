using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Product.Application.Abstractions;
using Product.Application.Exceptions;
using Product.Application.Products;
using Product.Infrastructure.Persistence;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Tests;

public sealed class ProductApiTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
{
    private static ProductRequest Request(string name = "Teclado") => new(name, "Teclado mecánico", 123.45m, 8);

    private async Task<ProductResponse> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/products", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }

    [Fact]
    public void AutoMapperConfigurationIsValid()
        => factory.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();

    [Fact]
    public async Task CreateReadAndUpdatePreserveValuesAndIdentity()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/products", Request("  Teclado  "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/products/{created.Id}", response.Headers.Location.ToString());
        Assert.Equal("Teclado", created.Name);
        Assert.Equal(123.45m, created.Price);
        Assert.Null(created.UpdatedAtUtc);
        Assert.Equal(created, await client.GetFromJsonAsync<ProductResponse>(response.Headers.Location));

        var update = await client.PutAsJsonAsync($"/api/products/{created.Id}", new ProductRequest("Mouse", "Inalámbrico", 99.99m, 0));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<ProductResponse>())!;
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.NotNull(updated.UpdatedAtUtc);
        Assert.Equal("Mouse", updated.Name);
        Assert.Equal(99.99m, updated.Price);
        Assert.Equal(0, updated.Stock);
        Assert.Equal(updated, await client.GetFromJsonAsync<ProductResponse>($"/api/products/{created.Id}"));
        Assert.Contains((await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!, product => product.Id == created.Id);
    }

    [Fact]
    public async Task DeleteHidesProductButRetainsItsDatabaseRecord()
    {
        using var client = factory.CreateClient();
        var product = await CreateAsync(client);
        var delete = await client.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await delete.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/products/{product.Id}")).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<ProductResponse>>("/api/products"))!, item => item.Id == product.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/products/{product.Id}", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/products/{product.Id}")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        Assert.False(await db.Products.AnyAsync(item => item.Id == product.Id));
        var retained = await db.Products.IgnoreQueryFilters().SingleAsync(item => item.Id == product.Id);
        Assert.True(retained.IsDeleted);
        Assert.NotNull(retained.DeletedAtUtc);
        Assert.Equal(TimeSpan.Zero, retained.DeletedAtUtc.Value.Offset);
        Assert.Equal(product.Name, retained.Name);
        Assert.Equal(product.Price, retained.Price.Amount);
        Assert.Equal(product.Stock, retained.Stock);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{broken json")]
    [InlineData("{\"name\":\" \",\"description\":\"d\",\"price\":1,\"stock\":1}")]
    [InlineData("{\"name\":\"n\",\"description\":\" \",\"price\":1,\"stock\":1}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":0,\"stock\":1}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":-1,\"stock\":1}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":1.001,\"stock\":1}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":10000000000000000,\"stock\":1}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":1,\"stock\":-1}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":1,\"stock\":1.5}")]
    [InlineData("{\"name\":\"n\",\"description\":\"d\",\"price\":1}")]
    public async Task InvalidInputReturnsValidationProblemAndDoesNotInsert(string json)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var before = await db.Products.IgnoreQueryFilters().CountAsync();
        var response = await client.PostAsync("/api/products", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.GetProperty("errors").EnumerateObject());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal(before, await db.Products.IgnoreQueryFilters().CountAsync());
    }

    [Theory]
    [InlineData(201, 10)]
    [InlineData(10, 2001)]
    public async Task TextLengthLimitsAreEnforced(int nameLength, int descriptionLength)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/products", new ProductRequest(new string('n', nameLength), new string('d', descriptionLength), 1, 0));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidUpdateDoesNotChangeStoredProduct()
    {
        using var client = factory.CreateClient();
        var product = await CreateAsync(client);
        var response = await client.PutAsJsonAsync($"/api/products/{product.Id}", new ProductRequest("Otro", "Otra", 12, -1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(product, await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}"));
    }

    [Fact]
    public async Task MissingResourcesReturnProblemDetails()
    {
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/products/{id}", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/products/{id}")).StatusCode);
        var response = await client.GetAsync($"/api/products/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.InternalServerError)]
    [InlineData(true, HttpStatusCode.Conflict)]
    public async Task MiddlewareMapsFailuresWithoutLeakingInternalDetails(bool conflict, HttpStatusCode expectedStatus)
    {
        using var failingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProductRepository>();
            services.AddScoped<IProductRepository>(_ => new FailingRepository(conflict
                ? new ConcurrencyConflictException()
                : new InvalidOperationException("sensitive-internal-detail")));
        }));
        using var client = failingFactory.CreateClient();
        var response = await client.GetAsync("/api/products");
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-internal-detail", text);
        Assert.DoesNotContain("stackTrace", text);
        Assert.Contains("traceId", text);
    }

    private sealed class FailingRepository(Exception exception) : IProductRepository
    {
        public Task<IReadOnlyList<ProductEntity>> ListAsync(CancellationToken cancellationToken) => throw exception;
        public Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw exception;
        public void Add(ProductEntity product) => throw exception;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw exception;
    }
}
