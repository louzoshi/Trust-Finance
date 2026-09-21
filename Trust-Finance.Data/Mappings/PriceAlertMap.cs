using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class PriceAlertMap : IEntityTypeConfiguration<PriceAlert>
{
    public void Configure(EntityTypeBuilder<PriceAlert> builder)
    {
        builder.ToTable("PriceAlerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Class).IsRequired();
        builder.Property(a => a.Direction).IsRequired();
        builder.Property(a => a.TargetPrice).IsRequired().HasPrecision(18, 8);
        builder.Property(a => a.TriggeredPrice).HasPrecision(18, 8);

        // Every quote refresh sweeps the armed alerts for the user being refreshed.
        builder.HasIndex(a => new { a.UserId, a.TriggeredAt });

        builder.HasOne(a => a.User)
            .WithMany(u => u.PriceAlerts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
