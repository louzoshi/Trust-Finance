using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class TransactionMap : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(255);
        builder.Property(t => t.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Type).IsRequired();

        // Every list and summary is "this user's transactions in this period".
        builder.HasIndex(t => new { t.UserId, t.Date });

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.User)
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Removing a recurrence stops the future; the rows it already posted are history
        // and stay, merely no longer traced back to it.
        builder.HasOne(t => t.Recurrence)
            .WithMany(r => r.Transactions)
            .HasForeignKey(t => t.RecurringTransactionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
