namespace Order.Application.Exceptions;

public sealed class OrderStateConflictException() : Exception("A confirmed order cannot be cancelled through this workflow.");
