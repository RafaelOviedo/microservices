namespace Product.Application.Exceptions;

public sealed class ProductNotFoundException(Guid id)
    : Exception($"No se encontró el producto {id}.");
