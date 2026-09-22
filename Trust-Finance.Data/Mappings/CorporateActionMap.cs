using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class CorporateActionMap : IEntityTypeConfiguration<CorporateAction>
{
    public void Configure(EntityTypeBuilder<CorporateAction> builder)
    {
        builder.ToTable("CorporateActions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Kind).IsRequired();
        builder.Property(a => a.Date).IsRequired();
        builder.Property(a => a.Factor).IsRequired().HasPrecision(18, 8);
        builder.Property(a => a.BonusUnitCost).HasPrecision(18, 8);

        builder.HasIndex(a => new { a.UserId, a.Ticker, a.Date });

        builder.HasOne(a => a.User)
            .WithMany(u => u.CorporateActions)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimistic concurrency: the version travels in the WHERE clause, so a write made
        // against a row somebody else has already changed updates nothing and is reported
        // instead of silently overwriting. See IVersioned.
        builder.Property(a => a.Version).IsConcurrencyToken();
    }
}
