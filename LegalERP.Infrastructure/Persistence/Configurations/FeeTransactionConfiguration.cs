using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class FeeTransactionConfiguration : IEntityTypeConfiguration<FeeTransaction>
{
    public void Configure(EntityTypeBuilder<FeeTransaction> builder)
    {
        builder.ToTable("fee_transactions");

        builder.HasKey(t => t.Id);

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.Property(t => t.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(t => t.ReceiptNumber).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasOne(t => t.Case)
            .WithMany(c => c.FeeTransactions)
            .HasForeignKey(t => t.CaseId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(t => t.Company)
            .WithMany(c => c.FeeTransactions)
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}
