using Order.Domain.Exceptions;

namespace Order.Domain.ValueObjects;

public sealed record CustomerSnapshot
{
    public const int NameMaxLength = 200;
    public Guid Id { get; }
    public string Name { get; }

    private CustomerSnapshot(Guid id, string name) => (Id, Name) = (id, name);

    public static CustomerSnapshot From(Guid id, string name)
    {
        if (id == Guid.Empty) throw new DomainValidationException("El ID del cliente es obligatorio.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
            throw new DomainValidationException("El nombre del cliente es obligatorio y admite hasta 200 caracteres.");
        return new CustomerSnapshot(id, name.Trim());
    }
}
