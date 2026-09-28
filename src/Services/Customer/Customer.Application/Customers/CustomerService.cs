using AutoMapper;
using FluentValidation;
using Customer.Application.Abstractions;
using Customer.Application.Exceptions;
using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Application.Customers;

public sealed class CustomerService(
    ICustomerRepository repository,
    IValidator<CustomerRequest> validator,
    IMapper mapper,
    TimeProvider clock) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> ListAsync(CancellationToken cancellationToken)
        => mapper.Map<List<CustomerResponse>>(await repository.ListAsync(cancellationToken));

    public async Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => mapper.Map<CustomerResponse>(await FindAsync(id, cancellationToken));

    public async Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var email = Email.From(request.Email!);
        await EnsureEmailAvailableAsync(email, null, cancellationToken);
        var customer = CustomerEntity.Create(request.Name!, email, ToAddress(request.Address!), clock.GetUtcNow());
        repository.Add(customer);
        await repository.SaveChangesAsync(cancellationToken);
        return mapper.Map<CustomerResponse>(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var customer = await FindAsync(id, cancellationToken);
        var email = Email.From(request.Email!);
        await EnsureEmailAvailableAsync(email, id, cancellationToken);
        customer.Update(request.Name!, email, ToAddress(request.Address!), clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return mapper.Map<CustomerResponse>(customer);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await FindAsync(id, cancellationToken);
        customer.Delete(clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEmailAvailableAsync(Email email, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await repository.IsEmailInUseAsync(email, excludedId, cancellationToken))
            throw new EmailAlreadyUsedException();
    }

    private static Address ToAddress(AddressRequest address)
        => Address.From(address.Street!, address.City!, address.State!, address.PostalCode!, address.Country!);

    private async Task<CustomerEntity> FindAsync(Guid id, CancellationToken cancellationToken)
        => await repository.GetByIdAsync(id, cancellationToken) ?? throw new CustomerNotFoundException(id);
}
