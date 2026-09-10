using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.ToTable("backup_records");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BackupType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(b => b.DestinationPath)
            .HasMaxLength(1000);

        builder.Property(b => b.ErrorMessage)
            .HasMaxLength(4000);

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
