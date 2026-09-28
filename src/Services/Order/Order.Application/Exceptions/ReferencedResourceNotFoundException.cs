namespace Order.Application.Exceptions;

public sealed class ReferencedResourceNotFoundException(string resource, Guid id)
    : Exception($"No se encontró un {resource} activo con ID {id}.");
