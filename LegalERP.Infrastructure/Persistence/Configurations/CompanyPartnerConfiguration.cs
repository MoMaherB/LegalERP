using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CompanyPartnerConfiguration : IEntityTypeConfiguration<CompanyPartner>
{
    public void Configure(EntityTypeBuilder<CompanyPartner> builder)
    {
        builder.ToTable("company_partners");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.FullName).IsRequired().HasMaxLength(300);
        builder.Property(p => p.OwnershipPercentage).HasPrecision(5, 2);
        builder.HasQueryFilter(p => !p.IsDeleted);

        // Explicit relationship to parent Company (uses existing CompanyId column)
        builder.HasOne(p => p.Company)
            .WithMany(c => c.Partners)
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Client)
            .WithMany(c => c.CompanyPartnerships)
            .HasForeignKey(p => p.ClientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}