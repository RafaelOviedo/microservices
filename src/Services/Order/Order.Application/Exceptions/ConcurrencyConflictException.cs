namespace Order.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("La orden fue modificada por otra operación. Volvé a consultarla antes de reintentar.", innerException);
