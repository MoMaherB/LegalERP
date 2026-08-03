using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.Property(n => n.Title).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();

        builder.HasOne(n => n.Case)
            .WithMany()
            .HasForeignKey(n => n.CaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(n => n.IsRead);
        builder.HasIndex(n => n.CreatedAt);
    }
}
