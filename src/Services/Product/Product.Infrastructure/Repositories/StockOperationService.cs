using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Product.Application.Exceptions;
using Product.Application.Stock;
using Product.Domain.Exceptions;
using Product.Domain.Stock;
using Product.Infrastructure.Persistence;

namespace Product.Infrastructure.Repositories;

// La transacción y los bloqueos viven en Infrastructure; los cambios de stock pasan por el dominio.
public sealed class StockOperationService(ProductDbContext db, IValidator<StockRequest> validator, TimeProvider clock)
    : IStockOperationService
{
    public async Task<StockOperationResponse> ApplyAsync(Guid id, StockRequest request, CancellationToken cancellationToken)
    {
        ValidateId(id);
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var lines = request.Items!.Select(x => x!).OrderBy(x => x.ProductId).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", lines.Select(line => $"{line.ProductId:N}:{line.Quantity}:{line.ExpectedUnitPrice.ToString("G29", CultureInfo.InvariantCulture)}")))));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(id, cancellationToken);
        var existing = await db.StockOperations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash is not null && existing.RequestHash != hash) throw new StockOperationConflictException();
            return Map(existing);
        }
        var ids = lines.Select(x => x.ProductId).ToArray();
        var products = await db.Products.FromSqlInterpolated(
            $"SELECT * FROM \"Products\" WHERE \"Id\" = ANY ({ids}) ORDER BY \"Id\" FOR UPDATE")
            .IgnoreQueryFilters().ToDictionaryAsync(x => x.Id, cancellationToken);
        string? rejection = null;
        if (lines.Any(line => !products.TryGetValue(line.ProductId, out var product) || product.IsDeleted))
            rejection = "ProductUnavailable";
        else if (lines.Any(line => products[line.ProductId].Price.Amount != line.ExpectedUnitPrice))
            rejection = "PriceChanged";
        else if (products.Values.All(product => product.Stock == 0))
            rejection = "NoStock";
        var now = clock.GetUtcNow();
        var operation = rejection is not null
            ? StockOperation.Rejected(id, hash, rejection, now)
            : StockOperation.Applied(id, hash, lines.Select(line => new StockAllocation(line.ProductId, line.Quantity,
                products[line.ProductId].DeductStock(line.Quantity, now))), now);
        db.StockOperations.Add(operation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(operation);
    }

    public async Task<StockOperationResponse> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(id, cancellationToken);
        var operation = await db.StockOperations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (operation is null)
        {
            // Esta marca impide que un Apply tardío descuente stock después de cancelar.
            operation = StockOperation.Cancelled(id, clock.GetUtcNow());
            db.StockOperations.Add(operation);
        }
        else if (operation.Status != StockOperationStatus.Cancelled)
        {
            if (operation.Status == StockOperationStatus.Applied)
            {
                var ids = operation.Allocations.Where(x => x.AcceptedQuantity > 0).Select(x => x.ProductId).ToArray();
                var products = await db.Products.FromSqlInterpolated(
                    $"SELECT * FROM \"Products\" WHERE \"Id\" = ANY ({ids}) ORDER BY \"Id\" FOR UPDATE")
                    .IgnoreQueryFilters().ToDictionaryAsync(x => x.Id, cancellationToken);
                foreach (var allocation in operation.Allocations.Where(x => x.AcceptedQuantity > 0))
                {
                    if (!products.TryGetValue(allocation.ProductId, out var product))
                        throw new InvalidOperationException("Falta un producto necesario para compensar stock.");
                    product.RestoreStock(allocation.AcceptedQuantity, clock.GetUtcNow());
                }
            }
            operation.Cancel();
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(operation);
    }

    public async Task<StockOperationResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var operation = await db.StockOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return operation is null ? null : Map(operation);
    }

    private Task<int> LockOperation(Guid id, CancellationToken cancellationToken)
        => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 0))", cancellationToken);
    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty) throw new DomainValidationException("El identificador de operación es obligatorio.");
    }
    private static StockOperationResponse Map(StockOperation value) => new(value.Id, value.Status.ToString(), value.Reason,
        value.Allocations.Select(x => new StockAllocationResponse(x.ProductId, x.RequestedQuantity, x.AcceptedQuantity)).ToArray());
}
