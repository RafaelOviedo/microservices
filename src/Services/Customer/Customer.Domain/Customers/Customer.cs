using Customer.Domain.Exceptions;
using Customer.Domain.ValueObjects;

namespace Customer.Domain.Customers;

public sealed class Customer
{
    public const int NameMaxLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public bool IsDeleted { get; private set; }
    public DateTimeOffset RegisteredAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public Guid Version { get; private set; }

    private Customer() { }

    public static Customer Create(string name, Email email, Address address, DateTimeOffset now)
    {
        Validate(name, email, address);
        return new Customer
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = email,
            Address = address,
            RegisteredAtUtc = NormalizeTimestamp(now),
            Version = Guid.NewGuid()
        };
    }

    public void Update(string name, Email email, Address address, DateTimeOffset now)
    {
        if (IsDeleted)
            throw new DomainValidationException("No se puede modificar un cliente eliminado.");

        // Validar antes de modificar el agregado evita cambios parciales en memoria.
        Validate(name, email, address);
        Name = name.Trim();
        Email = email;
        Address = address;
        UpdatedAtUtc = NormalizeTimestamp(now);
        Version = Guid.NewGuid();
    }

    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAtUtc = NormalizeTimestamp(now);
        UpdatedAtUtc = DeletedAtUtc;
        Version = Guid.NewGuid();
    }

    // Usar microsegundos mantiene las fechas idénticas al persistirlas y volver a leerlas.
    private static DateTimeOffset NormalizeTimestamp(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private static void Validate(string name, Email email, Address address)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            throw new DomainValidationException($"El nombre es obligatorio y admite hasta {NameMaxLength} caracteres.");
        if (email is null) throw new DomainValidationException("El email es obligatorio.");
        if (address is null) throw new DomainValidationException("La dirección es obligatoria.");
    }
}
