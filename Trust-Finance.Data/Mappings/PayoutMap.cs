using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class PayoutMap : IEntityTypeConfiguration<Payout>
{
    public void Configure(EntityTypeBuilder<Payout> builder)
    {
        builder.ToTable("Payouts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Kind).IsRequired();
        builder.Property(p => p.PaymentDate).IsRequired();
        builder.Property(p => p.Quantity).IsRequired().HasPrecision(18, 8);
        builder.Property(p => p.GrossAmount).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.WithheldTax).IsRequired().HasPrecision(18, 2);
        builder.Property(p => p.Note).HasMaxLength(200);

        // Listed newest first, and summed per ticker for yield on cost.
        builder.HasIndex(p => new { p.UserId, p.PaymentDate });
        builder.HasIndex(p => new { p.UserId, p.Ticker });

        builder.HasOne(p => p.User)
            .WithMany(u => u.Payouts)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
