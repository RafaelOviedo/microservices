namespace Order.Application.Abstractions;

public sealed record ProductData(Guid Id, string Name, decimal Price, int Stock);

public interface IProductClient
{
    Task<ProductData?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
