using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CompanyNameEn).HasMaxLength(500);
        builder.Property(c => c.TradeName).HasMaxLength(500);
        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.RegistrationNumber).HasMaxLength(100);
        builder.Property(c => c.Address).HasMaxLength(1000);

      

        // Soft-delete filter (TR-1.3): queries automatically exclude
        // deleted rows unless explicitly overridden with IgnoreQueryFilters().
        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasMany(c => c.Amendments)
            .WithOne()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Partners)
            .WithOne()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Trigram fuzzy-search index (TR-2.2, TR-7.1) — created via raw SQL
        // in the migration itself, since EF Core has no built-in GIN/trgm API.
    }
}
