using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Products;
using Product.Application.Stock;
using Product.Infrastructure.Persistence;

namespace Product.Tests;

public sealed class StockTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
{
    private static StockRequest Request(Guid product, int quantity, decimal price = 10m) => new([new(product, quantity, price)]);
    private async Task<ProductResponse> Create(HttpClient client, int stock = 10)
    {
        var response = await client.PostAsJsonAsync("/api/products", new ProductRequest("Stock test", "Descripción", 10, stock));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
    }
    private static async Task<StockOperationResponse> Apply(HttpClient client, Guid id, StockRequest request)
    {
        var response = await client.PutAsJsonAsync($"/api/stock-operations/{id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StockOperationResponse>())!;
    }
    private static async Task<int> Stock(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<ProductResponse>($"/api/products/{id}"))!.Stock;

    [Fact]
    public async Task ConcurrentReplayDeductsOnceAndRejectsDifferentPayload()
    {
        using var client = factory.CreateClient();
        var product = await Create(client);
        var id = Guid.NewGuid();
        var replies = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Apply(client, id, Request(product.Id, 3))));
        Assert.All(replies, result => Assert.Equal("Applied", result.Status));
        Assert.All(replies, result => Assert.Equal(3, Assert.Single(result.Allocations).AcceptedQuantity));
        Assert.Equal(7, await Stock(client, product.Id));
        var replay = await Apply(client, id, Request(product.Id, 3, 10.00m));
        Assert.Equal("Applied", replay.Status);
        var conflict = await client.PutAsJsonAsync($"/api/stock-operations/{id}", Request(product.Id, 4));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(7, await Stock(client, product.Id));
    }

    [Fact]
    public async Task ConcurrentDistinctOperationsNeverOversell()
    {
        using var client = factory.CreateClient();
        var product = await Create(client, 7);
        var replies = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Apply(client, Guid.NewGuid(), Request(product.Id, 4))));
        Assert.Equal(7, replies.Sum(result => result.Allocations.Sum(item => item.AcceptedQuantity)));
        Assert.Equal(0, await Stock(client, product.Id));
        Assert.Contains(replies, result => result.Status == "Rejected" && result.Reason == "NoStock");
    }

    [Fact]
    public async Task CancellationBeforeApplyPreventsLateDeduction()
    {
        using var client = factory.CreateClient();
        var product = await Create(client);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/stock-operations/{id}/cancel", null)).StatusCode);
        var response = await Apply(client, id, Request(product.Id, 3));
        Assert.Equal("Cancelled", response.Status);
        Assert.Equal(10, await Stock(client, product.Id));
    }

    [Fact]
    public async Task ConcurrentCancellationRestoresExactlyOnceIncludingSoftDeletedProducts()
    {
        using var client = factory.CreateClient();
        var product = await Create(client);
        var id = Guid.NewGuid();
        await Apply(client, id, Request(product.Id, 4));
        await client.DeleteAsync($"/api/products/{product.Id}");
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsync($"/api/stock-operations/{id}/cancel", null)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var retained = await db.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == product.Id);
        Assert.True(retained.IsDeleted);
        Assert.Equal(10, retained.Stock);
        Assert.Equal("Cancelled", (await Apply(client, id, Request(product.Id, 4))).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidBatchDoesNotDeductAnyProductAndRejectionIsStable(bool missing)
    {
        using var client = factory.CreateClient();
        var product = await Create(client);
        var other = await Create(client);
        var request = new StockRequest([new(product.Id, 3, 10), new(missing ? Guid.NewGuid() : other.Id, 3, missing ? 10 : 11)]);
        var id = Guid.NewGuid();
        var result = await Apply(client, id, request);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal(missing ? "ProductUnavailable" : "PriceChanged", result.Reason);
        Assert.Equal(10, await Stock(client, product.Id));
        Assert.Equal(10, await Stock(client, other.Id));
        Assert.Equal(result.Status, (await Apply(client, id, request)).Status);
    }

    [Fact]
    public async Task ApplyCancelRaceAlwaysEndsCancelledWithOriginalStock()
    {
        using var client = factory.CreateClient();
        var product = await Create(client);
        var id = Guid.NewGuid();
        await Task.WhenAll(client.PutAsJsonAsync($"/api/stock-operations/{id}", Request(product.Id, 4)),
            client.PostAsync($"/api/stock-operations/{id}/cancel", null));
        Assert.Equal("Cancelled", (await client.GetFromJsonAsync<StockOperationResponse>($"/api/stock-operations/{id}"))!.Status);
        Assert.Equal(10, await Stock(client, product.Id));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    public async Task InvalidStockRequestDoesNotWrite(int quantity, decimal price)
    {
        using var client = factory.CreateClient();
        var product = await Create(client);
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/stock-operations/{id}", Request(product.Id, quantity, price))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/stock-operations/{id}")).StatusCode);
        Assert.Equal(10, await Stock(client, product.Id));
    }
}
