namespace Customer.Application.Customers;

public sealed record CustomerResponse(Guid Id, string Name, string Email, AddressResponse Address,
    DateTimeOffset RegisteredAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record AddressResponse(string Street, string City, string State, string PostalCode, string Country);
