using Npgsql;
using Customer.Domain.ValueObjects;
using Customer.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Customer.Application.Abstractions;
using Customer.Application.Exceptions;
using Customer.Infrastructure.Persistence;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Infrastructure.Repositories;

public sealed class CustomerRepository(CustomerDbContext dbContext) : ICustomerRepository
{
    public async Task<IReadOnlyList<CustomerEntity>> ListAsync(CancellationToken cancellationToken)
        => await dbContext.Customers.AsNoTracking().OrderBy(customer => customer.Name).ThenBy(customer => customer.Id)
            .ToListAsync(cancellationToken);

    public Task<CustomerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<bool> IsEmailInUseAsync(Email email, Guid? excludedId, CancellationToken cancellationToken)
        => dbContext.Customers.AnyAsync(customer => customer.Email == email
            && (!excludedId.HasValue || customer.Id != excludedId.Value), cancellationToken);

    public void Add(CustomerEntity customer) => dbContext.Customers.Add(customer);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CustomerConfiguration.EmailIndexName })
        {
            throw new EmailAlreadyUsedException(exception);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }
}
