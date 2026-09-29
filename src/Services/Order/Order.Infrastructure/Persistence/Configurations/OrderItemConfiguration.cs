using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Orders;
using Order.Domain.ValueObjects;

namespace Order.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_ConfirmedQuantity", "\"ConfirmedQuantity\" IS NULL OR (\"ConfirmedQuantity\" >= 0 AND \"ConfirmedQuantity\" <= \"Quantity\")");
            table.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_OrderItems_UnitPrice", "\"UnitPrice\" > 0");
            table.HasCheckConstraint("CK_OrderItems_Subtotal", "\"Subtotal\" = \"UnitPrice\" * \"Quantity\"");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.ProductName).HasMaxLength(OrderItem.ProductNameMaxLength).IsRequired();
        builder.Property(item => item.UnitPrice).HasConversion(value => value.Amount, amount => Money.From(amount)).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.Subtotal).HasConversion(value => value.Amount, amount => Money.From(amount)).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(item => new { item.OrderId, item.ProductId }).IsUnique();
    }
}
