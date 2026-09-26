using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Product.Application.Products;

namespace Product.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, string? autoMapperLicenseKey = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<ProductRequestValidator>();
        services.AddAutoMapper(configuration =>
        {
            if (!string.IsNullOrWhiteSpace(autoMapperLicenseKey))
                configuration.LicenseKey = autoMapperLicenseKey;
        }, typeof(ProductMappingProfile).Assembly);
        services.AddScoped<IProductService, ProductService>();
        return services;
    }
}
