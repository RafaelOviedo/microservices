namespace Customer.Application.Exceptions;

public sealed class EmailAlreadyUsedException(Exception? innerException = null)
    : Exception("El email ya pertenece a un cliente activo.", innerException);
