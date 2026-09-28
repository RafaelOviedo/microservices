namespace Order.Application.Abstractions;

public sealed record CustomerData(Guid Id, string Name);

public interface ICustomerClient
{
    Task<CustomerData?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
