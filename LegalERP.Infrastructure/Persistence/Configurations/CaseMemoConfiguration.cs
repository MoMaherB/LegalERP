using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CaseMemoConfiguration : IEntityTypeConfiguration<CaseMemo>
{
    public void Configure(EntityTypeBuilder<CaseMemo> builder)
    {
        builder.ToTable("case_memos");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).IsRequired().HasMaxLength(300);
        builder.Property(m => m.Content).HasMaxLength(4000);

        builder.HasQueryFilter(m => !m.IsDeleted);

        builder.HasOne(m => m.Case)
            .WithMany(c => c.Memos)
            .HasForeignKey(m => m.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Document)
            .WithMany()
            .HasForeignKey(m => m.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
