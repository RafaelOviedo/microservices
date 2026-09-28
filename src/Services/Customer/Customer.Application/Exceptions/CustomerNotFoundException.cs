namespace Customer.Application.Exceptions;

public sealed class CustomerNotFoundException(Guid id)
    : Exception($"No se encontró el cliente {id}.");
