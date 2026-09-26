using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Abstractions;
using Product.Infrastructure.Persistence;
using Product.Infrastructure.Repositories;

namespace Product.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ProductDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProductRepository, ProductRepository>();
        return services;
    }
}
