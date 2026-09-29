namespace Order.Application.Exceptions;

public sealed class OrderNotFoundException(Guid id) : Exception($"Order {id} was not found.");
