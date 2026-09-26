namespace Product.Application.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken cancellationToken);
    Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken);
    Task<ProductResponse> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
