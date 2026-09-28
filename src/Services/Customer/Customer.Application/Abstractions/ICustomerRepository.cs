using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Application.Abstractions;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerEntity>> ListAsync(CancellationToken cancellationToken);
    Task<CustomerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> IsEmailInUseAsync(Email email, Guid? excludedId, CancellationToken cancellationToken);
    void Add(CustomerEntity customer);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
