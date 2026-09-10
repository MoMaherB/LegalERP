using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyAmendment> CompanyAmendments => Set<CompanyAmendment>();
    public DbSet<CompanyPartner> CompanyPartners => Set<CompanyPartner>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Case> Cases { get; set; } = null!;
    public DbSet<CaseParty> CaseParties { get; set; } = null!;
    public DbSet<CaseMemo> CaseMemos { get; set; } = null!;
    public DbSet<CaseHearing> CaseHearings { get; set; } = null!;
    public DbSet<FeeTransaction> FeeTransactions { get; set; } = null!;
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Attorney> Attorneys => Set<Attorney>();
    public DbSet<AttorneyClient> AttorneyClients => Set<AttorneyClient>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Must be first — sets up Identity tables

        modelBuilder.HasPostgresExtension("pg_trgm");

        // Applies every IEntityTypeConfiguration<T> class in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}