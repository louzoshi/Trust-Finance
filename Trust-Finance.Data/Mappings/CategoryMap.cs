using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustFinance.Domain.Entities;

namespace TrustFinance.Data.Mappings;

public class CategoryMap : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);

        // A category belongs to one user. The slug only has to be unique inside that
        // user's own set, so two people can each have a category called "mercado".
        builder.HasIndex(c => new { c.UserId, c.Slug }).IsUnique();

        // Deleting a user is not a feature; if it ever becomes one, it should be a
        // deliberate service-level operation rather than a cascade through here.
        builder.HasOne(c => c.User)
            .WithMany(u => u.Categories)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
