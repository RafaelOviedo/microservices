using System.Net.Mail;
using Customer.Domain.Exceptions;

namespace Customer.Domain.ValueObjects;

public sealed record Email
{
    public const int MaxLength = 254;
    public string Value { get; }
    private Email(string value) => Value = value;

    public static bool IsValid(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaxLength
            && !trimmed.Any(char.IsWhiteSpace)
            && MailAddress.TryCreate(trimmed, out var parsed)
            && string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase);
    }

    public static Email From(string value)
    {
        if (!IsValid(value)) throw new DomainValidationException("El email debe ser válido y admite hasta 254 caracteres.");
        return new Email(value.Trim().ToLowerInvariant());
    }
}
