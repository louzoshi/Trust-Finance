namespace TF.Data.Mappings
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TF.Models;
    
    public class TransactionMap : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("Transactions");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedOnAdd();
            builder.Property(t => t.Description).IsRequired().HasMaxLength(255);
            builder.Property(t => t.Amount).IsRequired().HasPrecision(18, 2);
            builder.Property(t => t.Date).IsRequired();
            builder.Property(t => t.Type).IsRequired();
            builder.HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}