namespace TF.Data.Mappings
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TF.Models;

    public class CategoryMap : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Slug).IsRequired().HasMaxLength(100);

            // A category belongs to one user. The slug only has to be unique inside that
            // user's own set, so two people can each have a category called "groceries".
            builder.HasIndex(c => new { c.UserId, c.Slug }).IsUnique();

            // Restrict, not Cascade: Transactions already cascades from both User and
            // Category, so cascading here too would give SQL Server two delete paths to
            // the same table ("may cause cycles or multiple cascade paths").
            builder.HasOne(c => c.User)
                .WithMany(u => u.Categories)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
