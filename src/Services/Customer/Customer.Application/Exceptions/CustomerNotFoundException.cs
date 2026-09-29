namespace Customer.Application.Exceptions;

public sealed class CustomerNotFoundException(Guid id)
    : Exception($"Customer {id} was not found.");
