namespace Product.Application.Exceptions;

public sealed class StockOperationConflictException()
    : Exception("This operation ID has already been used with different products, quantities, or prices.");
