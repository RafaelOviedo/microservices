namespace Order.Application.Exceptions;

public sealed class ReferencedResourceNotFoundException(string resource, Guid id)
    : Exception($"No active {resource} with ID {id} was found.");
