namespace Order.Application.Exceptions;

public sealed class OrderStateConflictException() : Exception("Una orden confirmada no puede cancelarse mediante este flujo.");
