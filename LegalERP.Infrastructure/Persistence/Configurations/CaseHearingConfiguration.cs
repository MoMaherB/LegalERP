using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CaseHearingConfiguration : IEntityTypeConfiguration<CaseHearing>
{
    public void Configure(EntityTypeBuilder<CaseHearing> builder)
    {
        builder.ToTable("case_hearings");

        builder.HasKey(h => h.Id);
        
        builder.HasQueryFilter(h => !h.IsDeleted);

        builder.HasOne(h => h.Case)
            .WithMany(c => c.Hearings)
            .HasForeignKey(h => h.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Performance Indexes (Ticket 04)
        builder.HasIndex(h => new { h.HearingDate, h.IsDeleted })
            .HasDatabaseName("IX_case_hearings_HearingDate_IsDeleted");

        builder.HasIndex(h => h.CaseId)
            .HasDatabaseName("IX_case_hearings_CaseId");
    }
}
