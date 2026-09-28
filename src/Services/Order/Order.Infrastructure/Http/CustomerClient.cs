using Order.Application.Abstractions;
using Order.Domain.ValueObjects;

namespace Order.Infrastructure.Http;

public sealed class CustomerClient(HttpClient client) : ICustomerClient
{
    public async Task<CustomerData?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var value = await ServiceHttpReader.GetAsync<Payload>(client, $"api/customers/{id}", "Customer",
            customer => customer.Id == id && !string.IsNullOrWhiteSpace(customer.Name)
                && customer.Name.Length <= CustomerSnapshot.NameMaxLength, cancellationToken);
        return value is null ? null : new CustomerData(value.Id, value.Name!);
    }

    private sealed record Payload(Guid Id, string? Name);
}
