namespace Customer.Application.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken cancellationToken);
    Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
