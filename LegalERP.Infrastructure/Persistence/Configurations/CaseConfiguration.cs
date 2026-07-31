using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CaseNumber).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CaseType).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.CourtName).HasMaxLength(200);
        builder.Property(c => c.JudgeName).HasMaxLength(200);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasMany(c => c.Parties)
            .WithOne()
            .HasForeignKey(p => p.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CaseNumber)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(c => c.Title)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
