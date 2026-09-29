namespace Product.Application.Exceptions;

public sealed class ProductNotFoundException(Guid id)
    : Exception($"Product {id} was not found.");
