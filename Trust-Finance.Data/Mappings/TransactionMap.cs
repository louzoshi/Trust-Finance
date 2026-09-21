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

        builder.Property(t => t.ExternalId).HasMaxLength(255);

        // Every list and summary is "this user's transactions in this period".
        builder.HasIndex(t => new { t.UserId, t.Date });

        // The bank's id is unique within one account: the same FITID imported twice is
        // the same line, and the database is the one place that can guarantee it.
        builder.HasIndex(t => new { t.UserId, t.AccountId, t.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");

        builder.HasIndex(t => t.TransferId);

        // An account with history cannot be dropped out from under it; the service
        // refuses first, the database refuses last.
        builder.HasOne(t => t.Account)
            .WithMany(a => a.Transactions)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

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
