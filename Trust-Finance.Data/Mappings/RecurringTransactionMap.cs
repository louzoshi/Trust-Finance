using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class RecurringTransactionMap : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        builder.ToTable("RecurringTransactions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Description).IsRequired().HasMaxLength(255);
        builder.Property(r => r.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(r => r.Type).IsRequired();
        builder.Property(r => r.Frequency).IsRequired();
        builder.Property(r => r.StartDate).IsRequired();
        builder.Property(r => r.NextDate).IsRequired();

        // Posting asks "which of this user's recurrences are due" on every page load.
        builder.HasIndex(r => new { r.UserId, r.NextDate });

        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Account)
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.User)
            .WithMany(u => u.Recurrences)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimistic concurrency: the version travels in the WHERE clause, so a write made
        // against a row somebody else has already changed updates nothing and is reported
        // instead of silently overwriting. See IVersioned.
        builder.Property(r => r.Version).IsConcurrencyToken();
    }
}
