namespace Product.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("The product was modified by another operation. Retrieve it again before retrying.", innerException);
