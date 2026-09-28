namespace Order.Application.Exceptions;

public sealed class OrderNotFoundException(Guid id) : Exception($"No se encontró la orden {id}.");
