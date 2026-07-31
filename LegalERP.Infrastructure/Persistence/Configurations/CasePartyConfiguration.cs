using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CasePartyConfiguration : IEntityTypeConfiguration<CaseParty>
{
    public void Configure(EntityTypeBuilder<CaseParty> builder)
    {
        builder.ToTable("case_parties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.NationalIdNumber).HasMaxLength(100);
        builder.Property(p => p.PhoneNumber).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasQueryFilter(p => !p.IsDeleted);

        // Explicit relationship to parent Case (uses existing CaseId column)
        builder.HasOne(p => p.Case)
            .WithMany(c => c.Parties)
            .HasForeignKey(p => p.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Document)
            .WithMany()
            .HasForeignKey(p => p.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Client)
            .WithMany(c => c.CaseParties)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
