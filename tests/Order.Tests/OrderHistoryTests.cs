using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Abstractions;
using Order.Application.Orders;
using Order.Domain.Orders;
using Order.Domain.ValueObjects;
using Order.Infrastructure.Http;
using Order.Infrastructure.Persistence;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Tests;

public sealed class OrderHistoryTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static OrderEntity NewOrder(Guid customer, DateTimeOffset date)
        => OrderEntity.Create(CustomerSnapshot.From(customer, "Cliente histórico"),
            [OrderItem.Create(Guid.NewGuid(), "Producto histórico", Money.From(12.34m), 2)], date);

    private async Task Save(params OrderEntity[] orders)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task HistoryPagesHaveStableOrderingCountsAndExcludeSoftDeletedOrders()
    {
        var customer = Guid.NewGuid();
        var date = DateTimeOffset.Parse("2026-09-01T12:00:00Z");
        var older = NewOrder(customer, date.AddDays(-1));
        var tied = new[] { NewOrder(customer, date), NewOrder(customer, date) };
        var deleted = NewOrder(customer, date.AddDays(1));
        deleted.Delete(date.AddDays(2));
        await Save(older, tied[0], tied[1], deleted, NewOrder(Guid.NewGuid(), date));
        using var client = factory.CreateClient();
        var first = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&pageSize=2&page=2"))!;
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(2, second.Page);
        Assert.Equal(tied.OrderByDescending(order => order.Id).Select(order => order.Id), first.Items.Select(order => order.Id));
        Assert.Equal(older.Id, Assert.Single(second.Items).Id);
        Assert.All(first.Items, order => Assert.Equal(24.68m, Assert.Single(order.Items).Subtotal));
        var beyond = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&pageSize=2&page=3"))!;
        Assert.Empty(beyond.Items);
        Assert.Equal(3, beyond.TotalCount);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.IgnoreQueryFilters().AnyAsync(order => order.Id == deleted.Id));
    }

    [Fact]
    public async Task FiltersCombineCustomerStatusAndInclusiveDatesWithTimezoneOffsets()
    {
        var customer = Guid.NewGuid();
        var date = DateTimeOffset.Parse("2026-09-01T12:00:00Z");
        var first = NewOrder(customer, date);
        var last = NewOrder(customer, date.AddHours(1));
        foreach (var order in new[] { first, last })
        {
            order.BeginConfirmation(date.AddDays(1));
            order.Confirm(order.Items.ToDictionary(item => item.ProductId, _ => 1), date.AddDays(1));
        }
        await Save(first, last, NewOrder(customer, date), NewOrder(customer, date.AddSeconds(-1)), NewOrder(Guid.NewGuid(), date));
        using var client = factory.CreateClient();
        var range = "from=2026-09-01T09:00:00-03:00&to=2026-09-01T10:00:00-03:00";
        var filtered = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&status=Confirmed&{range}"))!;
        Assert.Equal(2, filtered.TotalCount);
        Assert.Equal(new[] { last.Id, first.Id }, filtered.Items.Select(order => order.Id));
        Assert.All(filtered.Items, order =>
        {
            Assert.Equal("Confirmed", order.Status);
            Assert.Equal(12.34m, order.Total);
            Assert.Equal(1, Assert.Single(order.Items).Quantity);
        });
        var fromOnly = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&from=2026-09-01T13:00:00Z"))!;
        Assert.Equal(last.Id, Assert.Single(fromOnly.Items).Id);
        var toOnly = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&to=2026-09-01T11:59:59Z"))!;
        Assert.Single(toOnly.Items);
        var pending = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&status=PendingStockConfirmation"))!;
        Assert.Equal(2, pending.TotalCount);
        Assert.All(pending.Items, order => Assert.Equal("PendingStockConfirmation", order.Status));
    }

    [Fact]
    public async Task HistoryAndDetailRemainAvailableWithoutCallingOtherServices()
    {
        var order = NewOrder(Guid.NewGuid(), DateTimeOffset.UtcNow);
        await Save(order);
        var handler = new UnavailableHandler();
        using var isolated = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<ICustomerClient, CustomerClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddHttpClient<IProductClient, ProductClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddHttpClient<IStockClient, StockClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        }));
        using var client = isolated.CreateClient();
        var history = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={order.Customer.Id}"))!;
        Assert.Equal(order.Id, Assert.Single(history.Items).Id);
        Assert.Equal(20, history.PageSize);
        Assert.Equal(1, history.Page);
        Assert.Equal(1, history.TotalCount);
        Assert.Equal(order.Id, (await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}"))!.Id);
        var empty = (await client.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={Guid.NewGuid()}"))!;
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=999999999999999")]
    [InlineData("page=abc")]
    [InlineData("customerId=00000000-0000-0000-0000-000000000000")]
    [InlineData("customerId=abc")]
    [InlineData("status=Unknown")]
    [InlineData("status=99")]
    [InlineData("status=-1")]
    [InlineData("from=not-a-date")]
    [InlineData("to=not-a-date")]
    [InlineData("from=2026-09-02T00:00:00Z&to=2026-09-01T00:00:00Z")]
    public async Task InvalidFiltersReturnValidationProblems(string query)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/orders?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("errors", out _));
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task CompletePurchaseKeepsFinalHistoryAfterReferenceUpdatesDeletesAndHostRestart()
    {
        using var customers = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("ORDER_INTEGRATION_CUSTOMER_URL")!) };
        using var products = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("ORDER_INTEGRATION_PRODUCT_URL")!) };
        using var client = factory.CreateClient();
        object CustomerBody(string name) => new
        {
            name, email = $"{Guid.NewGuid():N}@example.com",
            address = new { street = "Calle 1", city = "Rosario", state = "Santa Fe", postalCode = "2000", country = "Argentina" }
        };
        var customer = await Create(customers, "/api/customers", CustomerBody("Cliente original"));
        var limited = await Create(products, "/api/products", new { name = "Teclado original", description = "Prueba", price = 12.34m, stock = 2 });
        var exhausted = await Create(products, "/api/products", new { name = "Mouse original", description = "Prueba", price = 5m, stock = 5 });
        var created = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(customer, [new(limited, 9), new(exhausted, 4)]));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var pending = (await created.Content.ReadFromJsonAsync<CreateOrderResponse>())!;
        Assert.Equal(new QuantityAdjustment(limited, 9, 2), Assert.Single(pending.QuantityAdjustments));
        Assert.Equal(44.68m, pending.Order.Total);
        Assert.Equal(HttpStatusCode.OK, (await products.PutAsJsonAsync($"/api/products/{limited}", new { name = "Teclado original", description = "Prueba", price = 12.34m, stock = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await products.PutAsJsonAsync($"/api/products/{exhausted}", new { name = "Mouse original", description = "Prueba", price = 5m, stock = 0 })).StatusCode);
        var response = await client.PostAsync($"/api/orders/{pending.Order.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var confirmed = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal("Confirmed", confirmed.Status);
        Assert.Equal(12.34m, confirmed.Total);
        Assert.Equal(new OrderItemResponse(limited, "Teclado original", 12.34m, 1, 12.34m), Assert.Single(confirmed.Items));
        Assert.Equal(0, (await products.GetFromJsonAsync<JsonElement>($"/api/products/{limited}")).GetProperty("stock").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await products.PutAsJsonAsync($"/api/products/{limited}", new { name = "Nuevo producto", description = "Editado", price = 99m, stock = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customers.PutAsJsonAsync($"/api/customers/{customer}", CustomerBody("Nuevo cliente"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await customers.DeleteAsync($"/api/customers/{customer}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await products.DeleteAsync($"/api/products/{limited}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await products.DeleteAsync($"/api/products/{exhausted}")).StatusCode);
        using var restarted = factory.WithWebHostBuilder(_ => { });
        using var reader = restarted.CreateClient();
        var history = (await reader.GetFromJsonAsync<OrderHistoryResponse>($"/api/orders?customerId={customer}&status=Confirmed"))!;
        var stored = Assert.Single(history.Items);
        Assert.Equal(confirmed.Id, stored.Id);
        Assert.Equal("Cliente original", stored.CustomerName);
        Assert.Equal(confirmed.Total, stored.Total);
        Assert.Equal(confirmed.ConfirmedAtUtc, stored.ConfirmedAtUtc);
        Assert.Equal(confirmed.OrderedAtUtc, stored.OrderedAtUtc);
        Assert.Equal(confirmed.Items.ToArray(), stored.Items.ToArray());
        await using var scope = restarted.Services.CreateAsyncScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.Include(order => order.Items).SingleAsync(order => order.Id == stored.Id);
        Assert.Equal(2, persisted.Items.Count);
        Assert.Equal(2, persisted.Items.Single(item => item.ProductId == limited).Quantity);
        Assert.Equal(1, persisted.Items.Single(item => item.ProductId == limited).ConfirmedQuantity);
        Assert.Equal(0, persisted.Items.Single(item => item.ProductId == exhausted).ConfirmedQuantity);
    }

    private static async Task<Guid> Create(HttpClient client, string path, object body)
    {
        var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new HttpRequestException("Los servicios externos no están disponibles en esta prueba.");
        }
    }
}
