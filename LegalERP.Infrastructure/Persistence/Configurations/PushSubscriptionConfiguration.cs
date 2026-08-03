using LegalERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscriptions");

        builder.HasKey(s => s.Id);

        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.Property(s => s.Endpoint).HasMaxLength(2000).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(500).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(500).IsRequired();
        builder.Property(s => s.DeviceName).HasMaxLength(200);

        builder.HasIndex(s => s.Endpoint).IsUnique();
    }
}
