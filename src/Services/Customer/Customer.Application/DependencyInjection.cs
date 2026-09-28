using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Customer.Application.Customers;

namespace Customer.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, string? autoMapperLicenseKey = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<CustomerRequestValidator>();
        services.AddAutoMapper(configuration =>
        {
            if (!string.IsNullOrWhiteSpace(autoMapperLicenseKey))
                configuration.LicenseKey = autoMapperLicenseKey;
        }, typeof(CustomerMappingProfile).Assembly);
        services.AddScoped<ICustomerService, CustomerService>();
        return services;
    }
}
