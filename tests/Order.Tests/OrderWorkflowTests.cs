using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Abstractions;
using Order.Application.Orders;
using Order.Infrastructure.Http;
using Order.Infrastructure.Persistence;

namespace Order.Tests;

public sealed class OrderWorkflowTests(OrderApiFactory factory) : IClassFixture<OrderApiFactory>
{
    private static HttpClient Remote(string variable) => new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable(variable)
            ?? throw new InvalidOperationException("Usá compose.order.tests.yaml para ejecutar la integración."))
    };
    private static object CustomerRequest(string name) => new
    {
        name, email = $"{Guid.NewGuid():N}@example.com",
        address = new { street = "Calle 1", city = "Rosario", state = "Santa Fe", postalCode = "2000", country = "Argentina" }
    };
    private static async Task<Guid> CreateCustomer(HttpClient client, string name = "Ana original")
    {
        var response = await client.PostAsJsonAsync("/api/customers", CustomerRequest(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static async Task<Guid> CreateProduct(HttpClient client, decimal price = 12.34m, int stock = 2)
    {
        var response = await client.PostAsJsonAsync("/api/products", new { name = "Teclado original", description = "Producto de prueba", price, stock });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static CreateOrderRequest Request(Guid customer, params OrderItemRequest[] items) => new(customer, items);
    private async Task<int> OrderCount()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Orders.IgnoreQueryFilters().CountAsync();
    }
    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("sensitive-upstream-detail", body.ToString());
    }

    [Fact]
    public void MappingConfigurationIsValid()
        => factory.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();

    [Fact]
    public async Task CreatesPendingOrderUsingRealHttpPricesAndStockAndKeepsHistoricalSnapshots()
    {
        using var customers = Remote("ORDER_INTEGRATION_CUSTOMER_URL");
        using var products = Remote("ORDER_INTEGRATION_PRODUCT_URL");
        using var client = factory.CreateClient();
        var customer = await CreateCustomer(customers);
        var limited = await CreateProduct(products);
        var available = await CreateProduct(products, 5, 10);
        var empty = await CreateProduct(products, 10, 0);
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = customer, customerName = "Nombre falso", total = 0.01m,
            items = new[] {
                new { productId = limited, quantity = 8, unitPrice = 0.01m, productName = "Falso" },
                new { productId = available, quantity = 3, unitPrice = 0.01m, productName = "Falso" },
                new { productId = empty, quantity = 1, unitPrice = 0.01m, productName = "Falso" }
            }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CreateOrderResponse>())!;
        Assert.Equal("PendingStockConfirmation", created.Order.Status);
        Assert.Equal("Ana original", created.Order.CustomerName);
        Assert.Equal(39.68m, created.Order.Total);
        Assert.Equal(2, created.Order.Items.Count);
        Assert.Equal(new QuantityAdjustment(limited, 8, 2), created.QuantityAdjustments.Single(x => x.ProductId == limited));
        Assert.Equal(new QuantityAdjustment(empty, 1, 0), created.QuantityAdjustments.Single(x => x.ProductId == empty));
        Assert.Equal(new OrderItemResponse(limited, "Teclado original", 12.34m, 2, 24.68m), created.Order.Items.Single(x => x.ProductId == limited));
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/orders/{created.Order.Id}", response.Headers.Location!.ToString());
        // Bloque 2 no modifica stock: una orden pendiente todavía no es una compra confirmada.
        Assert.Equal(2, (await products.GetFromJsonAsync<JsonElement>($"/api/products/{limited}")).GetProperty("stock").GetInt32());
        var productUpdate = await products.PutAsJsonAsync($"/api/products/{limited}", new { name = "Nombre nuevo", description = "Nueva", price = 99m, stock = 100 });
        Assert.Equal(HttpStatusCode.OK, productUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customers.PutAsJsonAsync($"/api/customers/{customer}", CustomerRequest("Ana nueva"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await customers.DeleteAsync($"/api/customers/{customer}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await products.DeleteAsync($"/api/products/{limited}")).StatusCode);
        var stored = (await client.GetFromJsonAsync<OrderResponse>(response.Headers.Location))!;
        Assert.Equal(created.Order.Id, stored.Id);
        Assert.Equal(created.Order.OrderedAtUtc, stored.OrderedAtUtc);
        Assert.Equal(created.Order.CustomerName, stored.CustomerName);
        Assert.Equal(created.Order.Total, stored.Total);
        Assert.Equal(created.Order.Items.ToArray(), stored.Items.ToArray());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeletedReferencesRejectTheWholeOrderWithoutSaving(bool deleteCustomer)
    {
        using var customers = Remote("ORDER_INTEGRATION_CUSTOMER_URL");
        using var products = Remote("ORDER_INTEGRATION_PRODUCT_URL");
        using var client = factory.CreateClient();
        var customer = await CreateCustomer(customers);
        var product = await CreateProduct(products);
        var valid = await CreateProduct(products);
        if (deleteCustomer) await customers.DeleteAsync($"/api/customers/{customer}");
        else await products.DeleteAsync($"/api/products/{product}");
        var before = await OrderCount();
        await AssertProblem(await client.PostAsJsonAsync("/api/orders", Request(customer, new(valid, 1), new(product, 1))), HttpStatusCode.NotFound);
        Assert.Equal(before, await OrderCount());
    }

    [Fact]
    public async Task NoAvailableStockReturns400WithoutSaving()
    {
        using var customers = Remote("ORDER_INTEGRATION_CUSTOMER_URL");
        using var products = Remote("ORDER_INTEGRATION_PRODUCT_URL");
        using var client = factory.CreateClient();
        var customer = await CreateCustomer(customers);
        var product = await CreateProduct(products, stock: 0);
        var before = await OrderCount();
        await AssertProblem(await client.PostAsJsonAsync("/api/orders", Request(customer, new OrderItemRequest(product, 1))), HttpStatusCode.BadRequest);
        Assert.Equal(before, await OrderCount());
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        yield return new object[] { new CreateOrderRequest(Guid.Empty, [new(product, 1)]) };
        yield return new object[] { new CreateOrderRequest(customer, null) };
        yield return new object[] { new CreateOrderRequest(customer, []) };
        yield return new object[] { new CreateOrderRequest(customer, [null]) };
        yield return new object[] { new CreateOrderRequest(customer, [new(Guid.Empty, 1)]) };
        yield return new object[] { new CreateOrderRequest(customer, [new(product, 0)]) };
        yield return new object[] { new CreateOrderRequest(customer, [new(product, -1)]) };
        yield return new object[] { new CreateOrderRequest(customer, [new(product, 1), new(product, 2)]) };
        yield return new object[] { new CreateOrderRequest(customer, Enumerable.Range(0, 101).Select(_ => new OrderItemRequest(Guid.NewGuid(), 1)).ToArray()) };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestsNeverCallDependenciesOrSave(CreateOrderRequest request)
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("HTTP no debe ejecutarse"));
        using var configured = WithHandler(handler);
        using var client = configured.CreateClient();
        var before = await OrderCount();
        await AssertProblem(await client.PostAsJsonAsync("/api/orders", request), HttpStatusCode.BadRequest);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(before, await OrderCount());
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"customerId\":\"invalid-guid\",\"items\":[]}")]
    public async Task MalformedInputReturns400(string json)
    {
        using var client = factory.CreateClient();
        var before = await OrderCount();
        await AssertProblem(await client.PostAsync("/api/orders", new StringContent(json, Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
        Assert.Equal(before, await OrderCount());
    }

    [Theory]
    [InlineData("Customer", "500", 502)]
    [InlineData("Customer", "network", 502)]
    [InlineData("Customer", "timeout", 504)]
    [InlineData("Customer", "malformed", 502)]
    [InlineData("Customer", "null", 502)]
    [InlineData("Customer", "wrong-id", 502)]
    [InlineData("Customer", "404", 404)]
    [InlineData("Product", "500", 502)]
    [InlineData("Product", "network", 502)]
    [InlineData("Product", "timeout", 504)]
    [InlineData("Product", "malformed", 502)]
    [InlineData("Product", "null", 502)]
    [InlineData("Product", "wrong-id", 502)]
    [InlineData("Product", "missing-stock", 502)]
    [InlineData("Product", "negative-stock", 502)]
    [InlineData("Product", "invalid-price", 502)]
    [InlineData("Product", "404", 404)]
    public async Task UpstreamFailuresDoNotSaveAndReturnSafeProblems(string service, string failure, int status)
    {
        var customer = Guid.NewGuid(); var product = Guid.NewGuid();
        var handler = new StubHandler(request =>
        {
            var isCustomer = request.RequestUri!.AbsolutePath.StartsWith("/api/customers/");
            if ((isCustomer ? "Customer" : "Product") != service)
                return Json(new { id = customer, name = "Ana" });
            return failure switch
            {
                "500" => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("sensitive-upstream-detail") },
                "404" => new HttpResponseMessage(HttpStatusCode.NotFound),
                "network" => throw new HttpRequestException("sensitive-upstream-detail"),
                "timeout" => throw new TaskCanceledException("sensitive-upstream-detail"),
                "malformed" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bad", Encoding.UTF8, "application/json") },
                "null" => Json<object?>(null),
                "wrong-id" => Json(new { id = Guid.NewGuid(), name = "Otro", price = 10, stock = 10 }),
                "missing-stock" => Json(new { id = product, name = "Teclado", price = 10 }),
                "negative-stock" => Json(new { id = product, name = "Teclado", price = 10, stock = -1 }),
                "invalid-price" => Json(new { id = product, name = "Teclado", price = 0.001m, stock = 1 }),
                _ => throw new InvalidOperationException()
            };
        });
        using var configured = WithHandler(handler);
        using var client = configured.CreateClient();
        var before = await OrderCount();
        await AssertProblem(await client.PostAsJsonAsync("/api/orders", Request(customer, new OrderItemRequest(product, 1))), (HttpStatusCode)status);
        Assert.Equal(before, await OrderCount());
    }

    [Fact]
    public async Task GetMissingOrSoftDeletedOrderReturns404WithoutRemoteCalls()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("HTTP no debe ejecutarse"));
        using var configured = WithHandler(handler);
        using var client = configured.CreateClient();
        await AssertProblem(await client.GetAsync($"/api/orders/{Guid.NewGuid()}"), HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = Order.Domain.Orders.Order.Create(Order.Domain.ValueObjects.CustomerSnapshot.From(Guid.NewGuid(), "Ana"),
            [Order.Domain.Orders.OrderItem.Create(Guid.NewGuid(), "Teclado", Order.Domain.ValueObjects.Money.From(1), 1)], DateTimeOffset.UtcNow);
        order.Delete(DateTimeOffset.UtcNow);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await AssertProblem(await client.GetAsync($"/api/orders/{order.Id}"), HttpStatusCode.NotFound);
        Assert.Equal(0, handler.Calls);
    }

    private WebApplicationFactory<Program> WithHandler(StubHandler handler)
        => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<ICustomerClient, CustomerClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddHttpClient<IProductClient, ProductClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        }));

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}
