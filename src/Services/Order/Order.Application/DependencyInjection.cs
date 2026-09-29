using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Orders;

namespace Order.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, string? autoMapperLicenseKey = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
        services.AddAutoMapper(configuration =>
        {
            if (!string.IsNullOrWhiteSpace(autoMapperLicenseKey))
                configuration.LicenseKey = autoMapperLicenseKey;
        }, typeof(OrderMappingProfile).Assembly);
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderProcessingService, OrderProcessingService>();
        return services;
    }
}
