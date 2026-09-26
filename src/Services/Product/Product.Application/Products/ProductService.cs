using AutoMapper;
using FluentValidation;
using Product.Application.Abstractions;
using Product.Application.Exceptions;
using Product.Domain.ValueObjects;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Application.Products;

public sealed class ProductService(
    IProductRepository repository,
    IValidator<ProductRequest> validator,
    IMapper mapper,
    TimeProvider clock) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken cancellationToken)
        => mapper.Map<List<ProductResponse>>(await repository.ListAsync(cancellationToken));

    public async Task<ProductResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => mapper.Map<ProductResponse>(await FindAsync(id, cancellationToken));

    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var product = ProductEntity.Create(request.Name!, request.Description!, Money.From(request.Price!.Value),
            request.Stock!.Value, clock.GetUtcNow());
        repository.Add(product);
        await repository.SaveChangesAsync(cancellationToken);
        return mapper.Map<ProductResponse>(product);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, ProductRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var product = await FindAsync(id, cancellationToken);
        product.Update(request.Name!, request.Description!, Money.From(request.Price!.Value),
            request.Stock!.Value, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return mapper.Map<ProductResponse>(product);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await FindAsync(id, cancellationToken);
        product.Delete(clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductEntity> FindAsync(Guid id, CancellationToken cancellationToken)
        => await repository.GetByIdAsync(id, cancellationToken) ?? throw new ProductNotFoundException(id);
}
