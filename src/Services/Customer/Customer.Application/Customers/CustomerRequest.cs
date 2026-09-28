namespace Customer.Application.Customers;

public sealed record CustomerRequest(string? Name, string? Email, AddressRequest? Address);
public sealed record AddressRequest(string? Street, string? City, string? State, string? PostalCode, string? Country);
