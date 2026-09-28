using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Customer.Application.Abstractions;
using Customer.Application.Exceptions;
using Customer.Domain.ValueObjects;
using Customer.Infrastructure.Persistence;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Tests;

public sealed class PersistenceTests(CustomerApiFactory factory) : IClassFixture<CustomerApiFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoveIsConvertedToSoftDeleteForSyncAndAsyncSaves(bool asyncSave)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var customer = CustomerEntity.Create("Original", Email.From($"{Guid.NewGuid():N}@example.com"), Address.From("Calle 1", "Rosario", "Santa Fe", "2000", "Argentina"), DateTimeOffset.UtcNow);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        var originalEmail = customer.Email;
        customer.Update("Cambio sin guardar", Email.From($"{Guid.NewGuid():N}@example.com"), Address.From("Otra", "Otra", "Otra", "5000", "Otro"), DateTimeOffset.UtcNow);
        db.Customers.Remove(customer);
        if (asyncSave) await db.SaveChangesAsync();
        else db.SaveChanges();
        db.ChangeTracker.Clear();

        Assert.False(await db.Customers.AnyAsync(item => item.Id == customer.Id));
        var retained = await db.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == customer.Id);
        Assert.True(retained.IsDeleted);
        Assert.NotNull(retained.DeletedAtUtc);
        Assert.Equal("Original", retained.Name);
        Assert.Equal(originalEmail, retained.Email);
        Assert.Equal(Address.From("Calle 1", "Rosario", "Santa Fe", "2000", "Argentina"), retained.Address);
    }

    [Fact]
    public async Task StaleWriteCannotOverwriteAConcurrentSoftDelete()
    {
        await using var firstScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var customer = CustomerEntity.Create("Concurrencia", Email.From($"{Guid.NewGuid():N}@example.com"), Address.From("Calle 1", "Rosario", "Santa Fe", "2000", "Argentina"), DateTimeOffset.UtcNow);
        first.Add(customer);
        await first.SaveChangesAsync(default);

        await using var secondScope = factory.Services.CreateAsyncScope();
        var second = secondScope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var stale = (await second.GetByIdAsync(customer.Id, default))!;
        customer.Delete(DateTimeOffset.UtcNow);
        await first.SaveChangesAsync(default);
        stale.Update("No debe guardarse", stale.Email, stale.Address, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync(default));

        await using var verification = factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<CustomerDbContext>();
        var retained = await db.Customers.IgnoreQueryFilters().SingleAsync(item => item.Id == customer.Id);
        Assert.True(retained.IsDeleted);
        Assert.Equal("Concurrencia", retained.Name);
    }
    [Fact]
    public async Task UniqueIndexProtectsAgainstRaceAfterBothPrechecksPass()
    {
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var email = Email.From($"{Guid.NewGuid():N}@example.com");
        var address = Address.From("Calle 1", "Rosario", "Santa Fe", "2000", "Argentina");
        Assert.False(await first.IsEmailInUseAsync(email, null, default));
        Assert.False(await second.IsEmailInUseAsync(email, null, default));
        first.Add(CustomerEntity.Create("Uno", email, address, DateTimeOffset.UtcNow));
        second.Add(CustomerEntity.Create("Dos", email, address, DateTimeOffset.UtcNow));
        await first.SaveChangesAsync(default);
        await Assert.ThrowsAsync<EmailAlreadyUsedException>(() => second.SaveChangesAsync(default));
        await using var verification = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await verification.ServiceProvider.GetRequiredService<CustomerDbContext>()
            .Customers.CountAsync(customer => customer.Email == email));
    }
}
