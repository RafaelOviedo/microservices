using AutoMapper;
using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Application.Customers;

public sealed class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile()
    {
        CreateMap<Address, AddressResponse>();
        CreateMap<CustomerEntity, CustomerResponse>()
            .ForCtorParam(nameof(CustomerResponse.Email), options => options.MapFrom(customer => customer.Email.Value));
    }
}
