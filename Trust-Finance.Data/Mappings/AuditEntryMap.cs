using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class AuditEntryMap : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Entity).IsRequired().HasMaxLength(60);
        builder.Property(a => a.Changes).HasMaxLength(AuditEntry.MaxChangesLength);

        // The trail is read one user at a time, newest first, and never joined to the row
        // it describes: a deleted transaction is exactly the case the trail exists for, so
        // there is no foreign key to the audited row and nothing cascades into this table.
        builder.HasIndex(a => new { a.UserId, a.OccurredAt });
    }
}
