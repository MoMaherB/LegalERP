using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LegalERP.Domain.Entities;

namespace LegalERP.Infrastructure.Persistence.Configurations;

public class AttorneyClientConfiguration : IEntityTypeConfiguration<AttorneyClient>
{
    public void Configure(EntityTypeBuilder<AttorneyClient> builder)
    {
        builder.ToTable("attorney_clients");

        builder.HasKey(ac => ac.Id);

        builder.HasQueryFilter(ac => !ac.IsDeleted);

        // Prevent duplicate links
        builder.HasIndex(ac => new { ac.AttorneyId, ac.ClientId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasOne(ac => ac.Attorney)
            .WithMany(a => a.AttorneyClients)
            .HasForeignKey(ac => ac.AttorneyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ac => ac.Client)
            .WithMany(c => c.AttorneyClients)
            .HasForeignKey(ac => ac.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
