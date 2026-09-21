using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class TradeMap : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Class).IsRequired();
        builder.Property(t => t.Side).IsRequired();

        // Eight decimals because crypto is bought in fractions; equities just never use them.
        builder.Property(t => t.Quantity).IsRequired().HasPrecision(18, 8);
        builder.Property(t => t.UnitPrice).IsRequired().HasPrecision(18, 8);
        builder.Property(t => t.Fees).IsRequired().HasPrecision(18, 2);
        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Note).HasMaxLength(200);

        // Positions are rebuilt by reading a user's whole ledger in date order.
        builder.HasIndex(t => new { t.UserId, t.Date });
        builder.HasIndex(t => new { t.UserId, t.Ticker });

        builder.HasOne(t => t.User)
            .WithMany(u => u.Trades)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
