using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class UserSettingsMap : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Theme).HasMaxLength(10);
        builder.Property(s => s.AccentColor).HasMaxLength(20);
        builder.Property(s => s.MarketDataToken).HasMaxLength(200);
        builder.Property(s => s.MonthlyContributionGoal).HasPrecision(18, 2);
        builder.Property(s => s.EmergencyFundGoal).HasPrecision(18, 2);

        // One settings row per user, enforced rather than assumed.
        builder.HasIndex(s => s.UserId).IsUnique();

        builder.HasOne(s => s.User)
            .WithOne(u => u.Settings)
            .HasForeignKey<UserSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
