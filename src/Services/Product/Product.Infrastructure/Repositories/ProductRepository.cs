using Microsoft.EntityFrameworkCore;
using Product.Application.Abstractions;
using Product.Application.Exceptions;
using Product.Infrastructure.Persistence;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Infrastructure.Repositories;

public sealed class ProductRepository(ProductDbContext dbContext) : IProductRepository
{
    public async Task<IReadOnlyList<ProductEntity>> ListAsync(CancellationToken cancellationToken)
        => await dbContext.Products.AsNoTracking().OrderBy(product => product.Name).ThenBy(product => product.Id)
            .ToListAsync(cancellationToken);

    public Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

    public void Add(ProductEntity product) => dbContext.Products.Add(product);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(exception);
        }
    }
}
