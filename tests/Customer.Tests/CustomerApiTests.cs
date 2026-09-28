using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Customer.Application.Abstractions;
using Customer.Application.Exceptions;
using Customer.Application.Customers;
using Customer.Domain.ValueObjects;
using Customer.Infrastructure.Persistence;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Tests;

public sealed class CustomerApiTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    private static CustomerRequest Request(string name = "Ana") => new(name, $"{Guid.NewGuid():N}@example.com",
        new AddressRequest("Calle 1", "Rosario", "Santa Fe", "2000", "Argentina"));

    private async Task<CustomerResponse> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/customers", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
    }

    [Fact]
    public void AutoMapperConfigurationIsValid()
        => factory.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();

    [Fact]
    public async Task CreateReadAndUpdatePreserveValuesAndIdentity()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/customers", Request("  Ana  "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/customers/{created.Id}", response.Headers.Location.ToString());
        Assert.Equal("Ana", created.Name);
        Assert.Equal("Calle 1", created.Address.Street);
        Assert.Null(created.UpdatedAtUtc);
        Assert.Equal(created, await client.GetFromJsonAsync<CustomerResponse>(response.Headers.Location));

        var update = await client.PutAsJsonAsync($"/api/customers/{created.Id}", Request("Ana María") with { Email = created.Email, Address = new AddressRequest("Calle 2", "Córdoba", "Córdoba", "5000", "Argentina") });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.RegisteredAtUtc, updated.RegisteredAtUtc);
        Assert.NotNull(updated.UpdatedAtUtc);
        Assert.Equal("Ana María", updated.Name);
        Assert.Equal("Córdoba", updated.Address.City);
        Assert.Equal(created.Email, updated.Email);
        Assert.Equal(updated, await client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{created.Id}"));
        Assert.Contains((await client.GetFromJsonAsync<List<CustomerResponse>>("/api/customers"))!, customer => customer.Id == created.Id);
    }

    [Fact]
    public async Task DeleteHidesCustomerButRetainsItsDatabaseRecord()
    {
        using var client = factory.CreateClient();
        var customer = await CreateAsync(client);
        var delete = await client.DeleteAsync($"/api/customers/{customer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(await delete.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/customers/{customer.Id}")).StatusCode);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<CustomerResponse>>("/api/customers"))!, item => item.Id == customer.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/customers/{customer.Id}", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/customers/{customer.Id}")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        Assert.False(await db.Customers.AnyAsync(item => item.Id == customer.Id));
        var retained = await db.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == customer.Id);
        Assert.True(retained.IsDeleted);
        Assert.NotNull(retained.DeletedAtUtc);
        Assert.Equal(TimeSpan.Zero, retained.DeletedAtUtc.Value.Offset);
        Assert.Equal(customer.Name, retained.Name);
        Assert.Equal(customer.Email, retained.Email.Value);
        Assert.Equal(customer.Address.Street, retained.Address.Street);
        var reuse = await client.PostAsJsonAsync("/api/customers", Request() with { Email = customer.Email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Created, reuse.StatusCode);
        Assert.NotEqual(customer.Id, (await reuse.Content.ReadFromJsonAsync<CustomerResponse>())!.Id);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{broken json")]
    [InlineData("{\"name\":\"Ana\",\"email\":\"ana@example.com\"}")]
    public async Task InvalidInputReturnsValidationProblemAndDoesNotInsert(string json)
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var before = await db.Customers.IgnoreQueryFilters().CountAsync();
        var response = await client.PostAsync("/api/customers", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.GetProperty("errors").EnumerateObject());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.Equal(before, await db.Customers.IgnoreQueryFilters().CountAsync());
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        var request = Request();
        yield return new object[] { request with { Name = " " } };
        yield return new object[] { request with { Name = new string('a', 201) } };
        yield return new object[] { request with { Email = "bad-email" } };
        yield return new object[] { request with { Email = "Ana <ana@example.com>" } };
        yield return new object[] { request with { Email = new string('a', 250) + "@example.com" } };
        yield return new object[] { request with { Address = null } };
        yield return new object[] { request with { Address = request.Address! with { Street = " " } } };
        yield return new object[] { request with { Address = request.Address! with { City = " " } } };
        yield return new object[] { request with { Address = request.Address! with { State = " " } } };
        yield return new object[] { request with { Address = request.Address! with { PostalCode = " " } } };
        yield return new object[] { request with { Address = request.Address! with { Country = " " } } };
        yield return new object[] { request with { Address = request.Address! with { Street = new string('a', 201) } } };
        yield return new object[] { request with { Address = request.Address! with { City = new string('a', 101) } } };
        yield return new object[] { request with { Address = request.Address! with { State = new string('a', 101) } } };
        yield return new object[] { request with { Address = request.Address! with { PostalCode = new string('a', 21) } } };
        yield return new object[] { request with { Address = request.Address! with { Country = new string('a', 101) } } };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task ValidationRejectsInvalidFields(CustomerRequest request)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/customers", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task EmailUniquenessIgnoresCaseAndSurroundingSpaces()
    {
        using var client = factory.CreateClient();
        var request = Request() with { Email = $"  Ana{Guid.NewGuid():N}@EXAMPLE.COM  " };
        var response = await client.PostAsJsonAsync("/api/customers", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var customer = (await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal(request.Email.Trim().ToLowerInvariant(), customer.Email);
        var duplicate = await client.PostAsJsonAsync("/api/customers", request with { Email = customer.Email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("application/problem+json", duplicate.Content.Headers.ContentType?.MediaType);
        var other = await CreateAsync(client);
        var update = await client.PutAsJsonAsync($"/api/customers/{other.Id}", Request() with { Email = customer.Email });
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(other, await client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{other.Id}"));
    }

    [Fact]
    public async Task ConcurrentCreatesReturnOneSuccessAndOneConflict()
    {
        using var client = factory.CreateClient();
        var request = Request();
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/customers", request), client.PostAsJsonAsync("/api/customers", request));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task InvalidUpdateDoesNotChangeStoredCustomer()
    {
        using var client = factory.CreateClient();
        var customer = await CreateAsync(client);
        var response = await client.PutAsJsonAsync($"/api/customers/{customer.Id}", Request("Otro") with { Email = "invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(customer, await client.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}"));
    }

    [Fact]
    public async Task MissingResourcesReturnProblemDetails()
    {
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/customers/{id}", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/customers/{id}")).StatusCode);
        var response = await client.GetAsync($"/api/customers/{id}");
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
            services.RemoveAll<ICustomerRepository>();
            services.AddScoped<ICustomerRepository>(_ => new FailingRepository(conflict
                ? new ConcurrencyConflictException()
                : new InvalidOperationException("sensitive-internal-detail")));
        }));
        using var client = failingFactory.CreateClient();
        var response = await client.GetAsync("/api/customers");
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sensitive-internal-detail", text);
        Assert.DoesNotContain("stackTrace", text);
        Assert.Contains("traceId", text);
    }

    private sealed class FailingRepository(Exception exception) : ICustomerRepository
    {
        public Task<IReadOnlyList<CustomerEntity>> ListAsync(CancellationToken cancellationToken) => throw exception;
        public Task<CustomerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw exception;
        public Task<bool> IsEmailInUseAsync(Email email, Guid? excludedId, CancellationToken cancellationToken) => throw exception;
        public void Add(CustomerEntity customer) => throw exception;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw exception;
    }
}
