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
            throw new DomainValidationException("A deleted product cannot be modified.");

        // Validate before modifying the aggregate to avoid partial changes in memory.
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
        if (IsDeleted) throw new DomainValidationException("The product has been deleted.");
        if (requested <= 0) throw new DomainValidationException("The quantity must be positive.");
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
            throw new DomainValidationException("This quantity cannot be restored to the current stock.");
        // Compensation must restore stock even if the product has been soft deleted.
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

    // Use microsecond precision to preserve timestamps when saving and reading them back.
    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static void Validate(string name, string description, Money price, int stock)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            throw new DomainValidationException($"The name is required and must not exceed {NameMaxLength} characters.");
        if (string.IsNullOrWhiteSpace(description) || description.Length > DescriptionMaxLength)
            throw new DomainValidationException($"The description is required and must not exceed {DescriptionMaxLength} characters.");
        if (price is null)
            throw new DomainValidationException("The price is required.");
        if (stock < 0)
            throw new DomainValidationException("Stock cannot be negative.");
    }
}
