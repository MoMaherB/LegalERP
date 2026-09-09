using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LegalERP.Domain.Entities;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class AttorneyConfiguration : IEntityTypeConfiguration<Attorney>
{
    public void Configure(EntityTypeBuilder<Attorney> builder)
    {
        builder.ToTable("attorneys");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AttorneyNumber).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasOne(a => a.Document)
            .WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.AttorneyNumber)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
