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
    }
}
