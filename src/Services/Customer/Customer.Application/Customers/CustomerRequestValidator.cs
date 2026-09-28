using FluentValidation;
using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Application.Customers;

public sealed class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(CustomerEntity.NameMaxLength).WithMessage("El nombre admite hasta 200 caracteres.");
        RuleFor(x => x.Email).Must(Email.IsValid).WithMessage("El email debe ser válido y admite hasta 254 caracteres.");
        RuleFor(x => x.Address).NotNull().WithMessage("La dirección es obligatoria.");
        When(x => x.Address is not null, () => RuleFor(x => x.Address!).SetValidator(new AddressRequestValidator()));
    }
}

public sealed class AddressRequestValidator : AbstractValidator<AddressRequest>
{
    public AddressRequestValidator()
    {
        RuleFor(x => x.Street).NotEmpty().MaximumLength(Address.StreetMaxLength);
        RuleFor(x => x.City).NotEmpty().MaximumLength(Address.LocalityMaxLength);
        RuleFor(x => x.State).NotEmpty().MaximumLength(Address.LocalityMaxLength);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(Address.PostalCodeMaxLength);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(Address.LocalityMaxLength);
    }
}
