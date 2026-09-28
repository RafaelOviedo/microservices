using Order.Application.Abstractions;
using Order.Domain.Orders;
using Order.Domain.ValueObjects;

namespace Order.Infrastructure.Http;

public sealed class ProductClient(HttpClient client) : IProductClient
{
    public async Task<ProductData?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var value = await ServiceHttpReader.GetAsync<Payload>(client, $"api/products/{id}", "Product",
            product => product.Id == id && !string.IsNullOrWhiteSpace(product.Name)
                && product.Name.Length <= OrderItem.ProductNameMaxLength && product.Stock is >= 0
                && product.Price is > 0 and <= Money.MaximumAmount
                && decimal.Round(product.Price.Value, 2) == product.Price.Value, cancellationToken);
        return value is null ? null : new ProductData(value.Id, value.Name!, value.Price!.Value, value.Stock!.Value);
    }

    private sealed record Payload(Guid Id, string? Name, decimal? Price, int? Stock);
}
