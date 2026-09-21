using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class FixedIncomeInvestmentMap : IEntityTypeConfiguration<FixedIncomeInvestment>
{
    public void Configure(EntityTypeBuilder<FixedIncomeInvestment> builder)
    {
        builder.ToTable("FixedIncomeInvestments");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Issuer).IsRequired().HasMaxLength(60);
        builder.Property(f => f.Kind).IsRequired();
        builder.Property(f => f.Index).IsRequired();

        // Six decimals on the rate: "110% do CDI" and "IPCA + 5,87%" both fit, and a
        // rounded rate compounds into a visibly wrong balance over a few years.
        builder.Property(f => f.Rate).IsRequired().HasPrecision(18, 6);
        builder.Property(f => f.Principal).IsRequired().HasPrecision(18, 2);
        builder.Property(f => f.PurchaseDate).IsRequired();
        builder.Property(f => f.MaturityDate).IsRequired();
        builder.Property(f => f.Note).HasMaxLength(200);

        // The book is read whole and ordered by maturity on every valuation.
        builder.HasIndex(f => new { f.UserId, f.MaturityDate });

        builder.HasOne(f => f.User)
            .WithMany(u => u.FixedIncome)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
