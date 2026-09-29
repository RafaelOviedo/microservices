namespace Product.Application.Exceptions;

public sealed class StockOperationConflictException()
    : Exception("Ese identificador de operación ya se utilizó con otros productos, cantidades o precios.");
