namespace Product.Application.Products;

public sealed record ProductRequest(string? Name, string? Description, decimal? Price, int? Stock);
