namespace Order.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("The order was modified by another operation. Retrieve it again before retrying.", innerException);
