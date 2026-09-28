using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Customer.Application.Abstractions;
using Customer.Infrastructure.Persistence;
using Customer.Infrastructure.Repositories;

namespace Customer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CustomerDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        return services;
    }
}
