using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.StoredFileName).IsRequired().HasMaxLength(500);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.OwnerType).IsRequired().HasMaxLength(100);

        builder.HasQueryFilter(d => !d.IsDeleted);

        builder.HasIndex(d => new { d.OwnerType, d.OwnerId });
    }
}
