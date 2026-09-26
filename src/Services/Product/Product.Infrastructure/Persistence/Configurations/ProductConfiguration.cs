using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Domain.ValueObjects;
using ProductEntity = Product.Domain.Products.Product;

namespace Product.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price", "\"Price\" > 0");
            table.HasCheckConstraint("CK_Products_Stock", "\"Stock\" >= 0");
            table.HasCheckConstraint("CK_Products_SoftDelete",
                "(NOT \"IsDeleted\" AND \"DeletedAtUtc\" IS NULL) OR (\"IsDeleted\" AND \"DeletedAtUtc\" IS NOT NULL)");
        });
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id).ValueGeneratedNever();
        builder.Property(product => product.Name).HasMaxLength(ProductEntity.NameMaxLength).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(ProductEntity.DescriptionMaxLength).IsRequired();
        builder.Property(product => product.Price)
            .HasConversion(money => money.Amount, amount => Money.From(amount))
            .HasPrecision(18, 2).IsRequired();
        builder.Property(product => product.Version).IsConcurrencyToken();
        builder.HasQueryFilter(product => !product.IsDeleted);
        builder.HasIndex(product => new { product.Name, product.Id }).HasFilter("NOT \"IsDeleted\"");
    }
}
