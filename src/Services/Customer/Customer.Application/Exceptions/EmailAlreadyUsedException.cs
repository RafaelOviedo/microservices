namespace Customer.Application.Exceptions;

public sealed class EmailAlreadyUsedException(Exception? innerException = null)
    : Exception("The email address is already assigned to an active customer.", innerException);
