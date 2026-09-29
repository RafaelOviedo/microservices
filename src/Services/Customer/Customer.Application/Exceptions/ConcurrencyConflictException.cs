namespace Customer.Application.Exceptions;

public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : Exception("The customer was modified by another operation. Retrieve it again before retrying.", innerException);
