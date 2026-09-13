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

        builder.Property(c => c.FileNumber).HasMaxLength(100);
        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CompanyNameEn).HasMaxLength(500);
        builder.Property(c => c.TradeName).HasMaxLength(500);
        builder.Property(c => c.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.RegistrationNumber).HasMaxLength(100);
        builder.Property(c => c.Address).HasMaxLength(1000);

      

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Many-Partners mode
        builder.Property(c => c.HasManyPartners).HasDefaultValue(false);
        builder.Property(c => c.PartnersText).HasMaxLength(10000);

        builder.HasOne(c => c.PartnersIdsDocument)
            .WithMany()
            .HasForeignKey(c => c.PartnersIdsDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.PartnersAttorneysDocument)
            .WithMany()
            .HasForeignKey(c => c.PartnersAttorneysDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.IncorporationDocument)
            .WithMany()
            .HasForeignKey(c => c.IncorporationDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Amendments)
            .WithOne()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Partners)
            .WithOne()
            .HasForeignKey(p => p.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Trigram fuzzy-search index (TR-2.2, TR-7.1)
        builder.HasIndex(c => c.CompanyName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(c => c.CompanyNameEn)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(c => c.TradeName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
