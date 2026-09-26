using ProductEntity = Product.Domain.Products.Product;

namespace Product.Application.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductEntity>> ListAsync(CancellationToken cancellationToken);
    Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(ProductEntity product);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
