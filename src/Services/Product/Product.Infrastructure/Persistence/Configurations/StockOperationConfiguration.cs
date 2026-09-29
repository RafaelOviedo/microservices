using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Product.Domain.Stock;

namespace Product.Infrastructure.Persistence.Configurations;

public sealed class StockOperationConfiguration : IEntityTypeConfiguration<StockOperation>
{
    public void Configure(EntityTypeBuilder<StockOperation> builder)
    {
        builder.ToTable("StockOperations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RequestHash).HasMaxLength(64);
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.Property(x => x.Allocations).HasColumnType("jsonb")
            .HasConversion(value => Serialize(value), value => Deserialize(value))
            .Metadata.SetValueComparer(new ValueComparer<IReadOnlyList<StockAllocation>>(
                (left, right) => Serialize(left!) == Serialize(right!),
                value => Serialize(value).GetHashCode(), value => Deserialize(Serialize(value))));
    }
    private static string Serialize(IReadOnlyList<StockAllocation> value) => JsonSerializer.Serialize(value);
    private static IReadOnlyList<StockAllocation> Deserialize(string value) => JsonSerializer.Deserialize<StockAllocation[]>(value)!;
}
