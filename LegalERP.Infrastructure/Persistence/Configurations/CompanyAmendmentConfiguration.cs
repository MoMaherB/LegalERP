using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CompanyAmendmentConfiguration : IEntityTypeConfiguration<CompanyAmendment>
{
    public void Configure(EntityTypeBuilder<CompanyAmendment> builder)
    {
        builder.ToTable("company_amendments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(500);
        builder.HasQueryFilter(a => !a.IsDeleted);

        // Matches BR-1.3: one company can't have two active amendments with the
        // same sequence number. We use a partial index so soft-deleted amendments
        // don't conflict with newly added ones.
        builder.HasIndex(a => new { a.CompanyId, a.SequenceNumber })
               .IsUnique()
               .HasFilter("\"IsDeleted\" = false");
    }
}