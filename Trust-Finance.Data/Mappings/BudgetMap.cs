using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class BudgetMap : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.MonthlyLimit).IsRequired().HasPrecision(18, 2);

        // One ceiling per category. Two would mean the app has to pick, and it cannot.
        builder.HasIndex(b => new { b.UserId, b.CategoryId }).IsUnique();

        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.User)
            .WithMany(u => u.Budgets)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimistic concurrency: the version travels in the WHERE clause, so a write made
        // against a row somebody else has already changed updates nothing and is reported
        // instead of silently overwriting. See IVersioned.
        builder.Property(b => b.Version).IsConcurrencyToken();
    }
}
