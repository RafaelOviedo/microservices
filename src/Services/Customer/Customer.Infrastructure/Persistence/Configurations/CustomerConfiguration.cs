using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Customer.Domain.ValueObjects;
using CustomerEntity = Customer.Domain.Customers.Customer;

namespace Customer.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<CustomerEntity>
{
    public const string EmailIndexName = "IX_Customers_Email";

    public void Configure(EntityTypeBuilder<CustomerEntity> builder)
    {
        builder.ToTable("Customers", table => table.HasCheckConstraint("CK_Customers_SoftDelete",
            "(NOT \"IsDeleted\" AND \"DeletedAtUtc\" IS NULL) OR (\"IsDeleted\" AND \"DeletedAtUtc\" IS NOT NULL)"));
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Id).ValueGeneratedNever();
        builder.Property(customer => customer.Name).HasMaxLength(CustomerEntity.NameMaxLength).IsRequired();
        builder.Property(customer => customer.Email).HasConversion(email => email.Value, value => Email.From(value))
            .HasMaxLength(Email.MaxLength).IsRequired();
        builder.ComplexProperty(customer => customer.Address, address =>
        {
            address.Property(x => x.Street).HasColumnName("Street").HasMaxLength(Address.StreetMaxLength).IsRequired();
            address.Property(x => x.City).HasColumnName("City").HasMaxLength(Address.LocalityMaxLength).IsRequired();
            address.Property(x => x.State).HasColumnName("State").HasMaxLength(Address.LocalityMaxLength).IsRequired();
            address.Property(x => x.PostalCode).HasColumnName("PostalCode").HasMaxLength(Address.PostalCodeMaxLength).IsRequired();
            address.Property(x => x.Country).HasColumnName("Country").HasMaxLength(Address.LocalityMaxLength).IsRequired();
        });
        builder.Property(customer => customer.Version).IsConcurrencyToken();
        builder.HasQueryFilter(customer => !customer.IsDeleted);
        builder.HasIndex(customer => customer.Email).IsUnique().HasDatabaseName(EmailIndexName).HasFilter("NOT \"IsDeleted\"");
        builder.HasIndex(customer => new { customer.Name, customer.Id }).HasFilter("NOT \"IsDeleted\"");
    }
}
