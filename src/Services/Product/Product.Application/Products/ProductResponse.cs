namespace Product.Application.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int Stock,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
