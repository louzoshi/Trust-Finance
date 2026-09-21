using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class WatchItemMap : IEntityTypeConfiguration<WatchItem>
{
    public void Configure(EntityTypeBuilder<WatchItem> builder)
    {
        builder.ToTable("Watchlist");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(w => w.Class).IsRequired();
        builder.Property(w => w.AddedAt).IsRequired();

        // Following the same ticker twice is a bug, not a preference.
        builder.HasIndex(w => new { w.UserId, w.Ticker }).IsUnique();

        builder.HasOne(w => w.User)
            .WithMany(u => u.Watchlist)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
