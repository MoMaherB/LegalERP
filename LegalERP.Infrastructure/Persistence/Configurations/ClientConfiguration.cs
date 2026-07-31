using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LegalERP.Domain.Entities;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FullName).IsRequired().HasMaxLength(300);
        builder.Property(c => c.FullNameEn).HasMaxLength(300);
        builder.Property(c => c.NationalIdNumber).HasMaxLength(100);
        builder.Property(c => c.PhoneNumber).HasMaxLength(100);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.Notes).HasMaxLength(2000);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasOne(c => c.NationalIdDocument)
            .WithMany()
            .HasForeignKey(c => c.NationalIdDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.AttorneyDocument)
            .WithMany()
            .HasForeignKey(c => c.AttorneyDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.FullName)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
            
        builder.HasIndex(c => c.FullNameEn)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
