using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Abstractions;
using Order.Infrastructure.Persistence;
using Order.Infrastructure.Repositories;
using Order.Infrastructure.Http;

namespace Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string customerBaseUrl, string productBaseUrl)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        var customerUri = ParseBaseAddress(customerBaseUrl, "Customer");
        var productUri = ParseBaseAddress(productBaseUrl, "Product");
        services.AddHttpClient<ICustomerClient, CustomerClient>(client =>
        {
            client.BaseAddress = customerUri;
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient<IProductClient, ProductClient>(client =>
        {
            client.BaseAddress = productUri;
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
    private static Uri ParseBaseAddress(string value, string service)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException($"Services:{service}:BaseUrl debe ser una URL HTTP o HTTPS válida.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
}
