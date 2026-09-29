using Product.Domain.Exceptions;
using Product.Domain.ValueObjects;

namespace Product.Domain.Products;

public sealed class Product
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Money Price { get; private set; } = null!;
    public int Stock { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public Guid Version { get; private set; }

    private Product() { }

    public static Product Create(string name, string description, Money price, int stock, DateTimeOffset now)
    {
        Validate(name, description, price, stock);
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description.Trim(),
            Price = price,
            Stock = stock,
            CreatedAtUtc = NormalizeTimestamp(now),
            Version = Guid.NewGuid()
        };
    }

    public void Update(string name, string description, Money price, int stock, DateTimeOffset now)
    {
        if (IsDeleted)
            throw new DomainValidationException("No se puede modificar un producto eliminado.");

        // Validar antes de modificar el agregado evita cambios parciales en memoria.
        Validate(name, description, price, stock);
        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Stock = stock;
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public int DeductStock(int requested, DateTimeOffset now)
    {
        if (IsDeleted) throw new DomainValidationException("El producto está eliminado.");
        if (requested <= 0) throw new DomainValidationException("La cantidad debe ser positiva.");
        var accepted = Math.Min(requested, Stock);
        if (accepted == 0) return 0;
        Stock -= accepted;
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
        return accepted;
    }

    public void RestoreStock(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0 || Stock > int.MaxValue - quantity)
            throw new DomainValidationException("No se puede devolver esa cantidad al stock actual.");
        // Una compensación debe devolver stock incluso si el producto recibió una baja lógica.
        Stock += quantity;
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = DeletedAtUtc;
        Version = Guid.NewGuid();
    }

    // Usar microsegundos mantiene las fechas idénticas al persistirlas y volver a leerlas.
    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static void Validate(string name, string description, Money price, int stock)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            throw new DomainValidationException($"El nombre es obligatorio y admite hasta {NameMaxLength} caracteres.");
        if (string.IsNullOrWhiteSpace(description) || description.Length > DescriptionMaxLength)
            throw new DomainValidationException($"La descripción es obligatoria y admite hasta {DescriptionMaxLength} caracteres.");
        if (price is null)
            throw new DomainValidationException("El precio es obligatorio.");
        if (stock < 0)
            throw new DomainValidationException("El stock no puede ser negativo.");
    }
}
