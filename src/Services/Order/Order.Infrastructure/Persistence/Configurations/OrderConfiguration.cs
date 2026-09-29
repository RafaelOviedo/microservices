using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.ValueObjects;
using OrderEntity = Order.Domain.Orders.Order;

namespace Order.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
{
    public void Configure(EntityTypeBuilder<OrderEntity> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Total", "\"Total\" > 0");
            table.HasCheckConstraint("CK_Orders_SoftDelete", "(NOT \"IsDeleted\" AND \"DeletedAtUtc\" IS NULL) OR (\"IsDeleted\" AND \"DeletedAtUtc\" IS NOT NULL)");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.ComplexProperty(order => order.Customer, customer =>
        {
            customer.Property(x => x.Id).HasColumnName("CustomerId").IsRequired();
            customer.Property(x => x.Name).HasColumnName("CustomerName").HasMaxLength(CustomerSnapshot.NameMaxLength).IsRequired();
        });
        builder.Property(order => order.Total).HasConversion(value => value.Amount, amount => Money.From(amount)).HasPrecision(18, 2).IsRequired();
        builder.Property(order => order.FailureReason).HasMaxLength(200);
        builder.Property(order => order.LastError).HasMaxLength(500);
        builder.HasIndex(order => new { order.Status, order.NextAttemptAtUtc });
        builder.Property(order => order.Version).IsConcurrencyToken();
        builder.HasQueryFilter(order => !order.IsDeleted);
        builder.HasIndex(order => new { order.OrderedAtUtc, order.Id }).HasFilter("NOT \"IsDeleted\"");
        // Conservar las relaciones hasta convertir Remove en una baja lógica.
        builder.HasMany(order => order.Items).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.ClientNoAction);
        builder.Navigation(order => order.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
