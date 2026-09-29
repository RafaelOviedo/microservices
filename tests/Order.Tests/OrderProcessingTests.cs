using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Order.API.Workers;
using Order.Application.Abstractions;
using Order.Application.Exceptions;
using Order.Application.Orders;
using Order.Domain.Orders;
using Order.Infrastructure.Http;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Tests;

public sealed class OrderProcessingTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static Uri ProductUri => new(Environment.GetEnvironmentVariable("ORDER_INTEGRATION_PRODUCT_URL")!);
    private static HttpClient Products() => new() { BaseAddress = ProductUri };
    private static HttpClient Customers() => new() { BaseAddress = new Uri(Environment.GetEnvironmentVariable("ORDER_INTEGRATION_CUSTOMER_URL")!) };

    private static async Task<Guid> CreateProduct(int stock, decimal price = 10m)
    {
        using var client = Products();
        var response = await client.PostAsJsonAsync("/api/products", new { name = "Producto saga", description = "Prueba", price, stock });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private async Task<OrderResponse> CreateOrder(Guid product, int quantity)
    {
        using var customers = Customers();
        var customerResponse = await customers.PostAsJsonAsync("/api/customers", new
        {
            name = "Cliente saga", email = $"{Guid.NewGuid():N}@example.com",
            address = new { street = "Calle 1", city = "Rosario", state = "Santa Fe", postalCode = "2000", country = "Argentina" }
        });
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customer = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(customer, [new(product, quantity)]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateOrderResponse>())!.Order;
    }
    private static async Task<int> Stock(Guid product)
    {
        using var client = Products();
        return (await client.GetFromJsonAsync<JsonElement>($"/api/products/{product}")).GetProperty("stock").GetInt32();
    }
    private static async Task<OrderResponse> Process(HttpClient client, Guid id, string action, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await client.PostAsync($"/api/orders/{id}/{action}", null);
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    [Fact]
    public async Task ConcurrentConfirmationsOfSameOrderDeductOnceAndCannotCancelConfirmedPurchase()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using var client = factory.CreateClient();
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Process(client, order.Id, "confirm")));
        Assert.All(results, result => Assert.Equal("Confirmed", result.Status));
        Assert.All(results, result => Assert.Equal(40m, result.Total));
        Assert.NotNull(results[0].ConfirmedAtUtc);
        Assert.Equal(6, await Stock(product));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/orders/{order.Id}/cancel", null)).StatusCode);
        Assert.Equal(6, await Stock(product));
    }

    [Fact]
    public async Task ConcurrentOrdersClampFinalQuantitiesAndNeverOversell()
    {
        var product = await CreateProduct(7);
        var orders = new[] { await CreateOrder(product, 4), await CreateOrder(product, 4), await CreateOrder(product, 4) };
        using var client = factory.CreateClient();
        var results = await Task.WhenAll(orders.Select(order => Process(client, order.Id, "confirm")));
        var confirmed = results.Where(x => x.Status == "Confirmed").ToArray();
        Assert.Equal(2, confirmed.Length);
        Assert.Equal(7, confirmed.Sum(order => order.Items.Sum(item => item.Quantity)));
        Assert.Equal(70m, confirmed.Sum(order => order.Total));
        Assert.Single(results, result => result.Status == "Rejected" && result.FailureReason == "NoStock");
        Assert.Equal(0, await Stock(product));
        foreach (var order in confirmed)
        {
            var stored = (await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!;
            Assert.Equal(order.Total, stored.Total);
            Assert.Equal(order.Items.ToArray(), stored.Items.ToArray());
        }
    }

    [Fact]
    public async Task CancellingPendingOrderIsIdempotentAndBlocksLateConfirmation()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using var client = factory.CreateClient();
        Assert.Equal("Cancelled", (await Process(client, order.Id, "cancel")).Status);
        Assert.Equal("Cancelled", (await Process(client, order.Id, "cancel")).Status);
        Assert.Equal("Cancelled", (await Process(client, order.Id, "confirm")).Status);
        Assert.Equal(10, await Stock(product));
    }

    [Fact]
    public async Task ConfirmCancelRaceHasOneConsistentTerminalOutcome()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(client.PostAsync($"/api/orders/{order.Id}/confirm", null), client.PostAsync($"/api/orders/{order.Id}/cancel", null));
        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var stored = (await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!;
        Assert.Contains(stored.Status, new[] { "Confirmed", "Cancelled" });
        Assert.Equal(stored.Status == "Confirmed" ? 6 : 10, await Stock(product));
    }

    [Fact]
    public async Task PriceChangesRejectConfirmationWithoutDeductingStock()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using var products = Products();
        await products.PutAsJsonAsync($"/api/products/{product}", new { name = "Nuevo", description = "Prueba", price = 11, stock = 10 });
        using var client = factory.CreateClient();
        var result = await Process(client, order.Id, "confirm");
        Assert.Equal("Rejected", result.Status);
        Assert.Equal("PriceChanged", result.FailureReason);
        Assert.Equal(10, await Stock(product));
    }

    [Fact]
    public async Task LostApplyResponseIsRecoveredByANewHostWithoutDoubleDeduction()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using (var failed = WithLostResponses(new FaultPlan { ApplyLoss = 1 }))
        using (var client = failed.CreateClient())
        {
            var response = await Process(client, order.Id, "confirm", HttpStatusCode.Accepted);
            Assert.Equal("Confirming", response.Status);
            Assert.Equal(6, await Stock(product));
        }
        using var recovery = WithWorker();
        using var recoveredClient = recovery.CreateClient();
        var recovered = await WaitFor(recoveredClient, order.Id, "Confirmed");
        Assert.Equal(40m, recovered.Total);
        Assert.Equal(6, await Stock(product));
    }

    [Fact]
    public async Task FailureSavingOrderAfterRemoteCommitIsRecoveredFromDurableIntent()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using (var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.AddDbContext<OrderDbContext>(options => options
                .UseNpgsql(Environment.GetEnvironmentVariable("ORDER_TEST_CONNECTION"))
                .AddInterceptors(new FailConfirmationSave()));
        })))
        using (var client = failing.CreateClient())
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync($"/api/orders/{order.Id}/confirm", null)).StatusCode);
            Assert.Equal("Confirming", (await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!.Status);
            Assert.Equal(6, await Stock(product));
        }
        using var recovery = WithWorker();
        using var recoveredClient = recovery.CreateClient();
        await WaitFor(recoveredClient, order.Id, "Confirmed");
        Assert.Equal(6, await Stock(product));
    }

    [Fact]
    public async Task LostCancellationResponseIsRecoveredWithoutRestoringStockTwice()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using (var failing = WithLostResponses(new FaultPlan { ApplyLoss = 1, CancelLoss = 1 }))
        using (var client = failing.CreateClient())
        {
            await Process(client, order.Id, "confirm", HttpStatusCode.Accepted);
            Assert.Equal(6, await Stock(product));
            Assert.Equal("CompensationPending", (await Process(client, order.Id, "cancel", HttpStatusCode.Accepted)).Status);
            Assert.Equal(10, await Stock(product));
        }
        using var recovery = WithWorker();
        using var recoveredClient = recovery.CreateClient();
        await WaitFor(recoveredClient, order.Id, "Cancelled");
        await Process(recoveredClient, order.Id, "cancel");
        await Process(recoveredClient, order.Id, "confirm");
        Assert.Equal(10, await Stock(product));
    }

    [Fact]
    public async Task CustomerDeletedWhileConfirmationWasUncertainTriggersCompensation()
    {
        var product = await CreateProduct(10);
        var order = await CreateOrder(product, 4);
        using (var failing = WithLostResponses(new FaultPlan { ApplyLoss = 1 }))
        using (var client = failing.CreateClient()) await Process(client, order.Id, "confirm", HttpStatusCode.Accepted);
        Assert.Equal(6, await Stock(product));
        using var customers = Customers();
        await customers.DeleteAsync($"/api/customers/{order.CustomerId}");
        using var recovery = WithWorker();
        using var recoveredClient = recovery.CreateClient();
        var cancelled = await WaitFor(recoveredClient, order.Id, "Cancelled");
        Assert.Equal("CustomerUnavailable", cancelled.FailureReason);
        Assert.Equal(10, await Stock(product));
    }

    [Fact]
    public async Task MissingOrderCannotStartStockOperations()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/orders/{Guid.NewGuid()}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/orders/{Guid.NewGuid()}/cancel", null)).StatusCode);
    }

    private WebApplicationFactory<Program> WithWorker() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.AddHostedService<OrderRecoveryWorker>()));

    private WebApplicationFactory<Program> WithLostResponses(FaultPlan plan) => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(plan);
            services.AddHttpClient<LosingResponsesClient>(client => client.BaseAddress = ProductUri);
            services.AddScoped<IStockClient>(provider => provider.GetRequiredService<LosingResponsesClient>());
        }));

    private static async Task<OrderResponse> WaitFor(HttpClient client, Guid id, string status)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var order = (await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{id}"))!;
            if (order.Status == status) return order;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Order {id} did not recover to {status}.");
    }

    private sealed class FaultPlan { public int ApplyLoss; public int CancelLoss; }
    private sealed class LosingResponsesClient(HttpClient http, FaultPlan plan) : IStockClient
    {
        public async Task<StockResult> ApplyAsync(Guid id, IReadOnlyList<StockLine> items, CancellationToken cancellationToken)
        {
            var result = await new StockClient(http).ApplyAsync(id, items, cancellationToken);
            if (Interlocked.Exchange(ref plan.ApplyLoss, 0) == 1) throw new UpstreamServiceException("Product", timeout: true);
            return result;
        }
        public async Task<StockResult> CancelAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await new StockClient(http).CancelAsync(id, cancellationToken);
            if (Interlocked.Exchange(ref plan.CancelLoss, 0) == 1) throw new UpstreamServiceException("Product", timeout: true);
            return result;
        }
    }
    private sealed class FailConfirmationSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<OrderEntity>().Any(entry => entry.State == EntityState.Modified && entry.Entity.Status == OrderStatus.Confirmed))
                throw new InvalidOperationException("Simulated persistence failure after the remote stock deduction.");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
