using FluentValidation;
using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Application.Customers;

public sealed class CustomerRequestValidator : AbstractValidator<CustomerRequest>
{
    public CustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("The name is required.")
            .MaximumLength(CustomerEntity.NameMaxLength).WithMessage("The name must not exceed 200 characters.");
        RuleFor(x => x.Email).Must(Email.IsValid).WithMessage("The email address must be valid and must not exceed 254 characters.");
        RuleFor(x => x.Address).NotNull().WithMessage("The address is required.");
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
