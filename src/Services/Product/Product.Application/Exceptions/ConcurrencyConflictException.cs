namespace Product.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("El producto fue modificado por otra operación. Volvé a consultarlo antes de reintentar.", innerException);
