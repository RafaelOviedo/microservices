using Customer.Domain.Exceptions;

namespace Customer.Domain.ValueObjects;

public sealed record Address
{
    public const int StreetMaxLength = 200;
    public const int LocalityMaxLength = 100;
    public const int PostalCodeMaxLength = 20;
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    private Address(string street, string city, string state, string postalCode, string country)
        => (Street, City, State, PostalCode, Country) = (street, city, state, postalCode, country);

    public static Address From(string street, string city, string state, string postalCode, string country)
    {
        Validate(street, StreetMaxLength, "street");
        Validate(city, LocalityMaxLength, "city");
        Validate(state, LocalityMaxLength, "state");
        Validate(postalCode, PostalCodeMaxLength, "postal code");
        Validate(country, LocalityMaxLength, "country");
        return new Address(street.Trim(), city.Trim(), state.Trim(), postalCode.Trim(), country.Trim());
    }

    private static void Validate(string value, int maximum, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new DomainValidationException($"The {field} field is required and must not exceed {maximum} characters.");
    }
}
