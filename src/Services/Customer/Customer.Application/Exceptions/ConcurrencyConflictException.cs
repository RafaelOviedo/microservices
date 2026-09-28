namespace Customer.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("El cliente fue modificado por otra operación. Volvé a consultarlo antes de reintentar.", innerException);
