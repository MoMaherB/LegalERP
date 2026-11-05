using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegalERP.Tests.Infrastructure;

public class DatabaseIndexTests
{
    private static readonly DbContextOptions<ApplicationDbContext> MetadataOptions =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_inspection;Username=postgres;Password=none")
            .Options;

    private static ApplicationDbContext CreateMetadataContext() => new(MetadataOptions);

    private static void AssertHasIndex<TEntity>(params string[] propertyNames)
    {
        using var context = CreateMetadataContext();
        var entityType = context.Model.FindEntityType(typeof(TEntity));
        entityType.Should().NotBeNull();

        var hasIndex = entityType!.GetIndexes().Any(idx =>
            idx.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

        hasIndex.Should().BeTrue($"{typeof(TEntity).Name} should have an index on ({string.Join(", ", propertyNames)})");
    }

    [Fact]
    public void CaseHearing_HasCompositeIndexOn_HearingDate_And_IsDeleted()
    {
        AssertHasIndex<CaseHearing>(nameof(CaseHearing.HearingDate), nameof(CaseHearing.IsDeleted));
    }

    [Fact]
    public void CaseHearing_HasIndexOn_CaseId()
    {
        AssertHasIndex<CaseHearing>(nameof(CaseHearing.CaseId));
    }

    [Fact]
    public void Case_HasCompositeIndexOn_Status_And_IsDeleted()
    {
        AssertHasIndex<Case>(nameof(Case.Status), nameof(Case.IsDeleted));
    }

    [Fact]
    public void Case_HasIndexOn_FilingDate()
    {
        AssertHasIndex<Case>(nameof(Case.FilingDate));
    }

    [Fact]
    public void Case_HasIndexOn_CaseNumber()
    {
        AssertHasIndex<Case>(nameof(Case.CaseNumber));
    }

    [Fact]
    public void Client_HasIndexOn_NationalIdNumber()
    {
        AssertHasIndex<Client>(nameof(Client.NationalIdNumber));
    }

    [Fact]
    public void Client_HasCompositeIndexOn_FullName_And_IsDeleted()
    {
        AssertHasIndex<Client>(nameof(Client.FullName), nameof(Client.IsDeleted));
    }

    [Fact]
    public void Company_HasIndexOn_RegistrationNumber()
    {
        AssertHasIndex<Company>(nameof(Company.RegistrationNumber));
    }

    [Fact]
    public void Company_HasIndexOn_Category()
    {
        AssertHasIndex<Company>(nameof(Company.Category));
    }

    [Fact]
    public async Task QueryExecution_FilteringByIndexedFields_ReturnsAccurateResults()
    {
        var dbName = "IndexQueryExecution_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new ApplicationDbContext(options);

        // Seed test entities
        var activeCase = new Case
        {
            Id = Guid.NewGuid(),
            CaseNumber = "C-101",
            Title = "Active Case",
            Status = CaseStatus.Active,
            FilingDate = new DateOnly(2026, 3, 1),
            IsDeleted = false
        };
        var closedCase = new Case
        {
            Id = Guid.NewGuid(),
            CaseNumber = "C-102",
            Title = "Closed Case",
            Status = CaseStatus.Closed,
            FilingDate = new DateOnly(2026, 1, 1),
            IsDeleted = false
        };
        var deletedCase = new Case
        {
            Id = Guid.NewGuid(),
            CaseNumber = "C-103",
            Title = "Deleted Case",
            Status = CaseStatus.Active,
            FilingDate = new DateOnly(2026, 2, 1),
            IsDeleted = true
        };
        db.Cases.AddRange(activeCase, closedCase, deletedCase);

        var client1 = new Client
        {
            Id = Guid.NewGuid(),
            FullName = "Ahmed Kamal",
            NationalIdNumber = "29001011234501",
            IsDeleted = false
        };
        var client2 = new Client
        {
            Id = Guid.NewGuid(),
            FullName = "Mahmoud Hassan",
            NationalIdNumber = "29001011234502",
            IsDeleted = false
        };
        db.Clients.AddRange(client1, client2);

        var comp1 = new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = "Company A",
            Category = CompanyCategory.CapitalCompany,
            RegistrationNumber = "CR-101",
            IsDeleted = false
        };
        var comp2 = new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = "Company B",
            Category = CompanyCategory.Partnership,
            RegistrationNumber = "CR-102",
            IsDeleted = false
        };
        db.Companies.AddRange(comp1, comp2);

        var hearing1 = new CaseHearing
        {
            Id = Guid.NewGuid(),
            CaseId = activeCase.Id,
            HearingDate = new DateOnly(2026, 5, 10),
            IsDeleted = false
        };
        var hearing2 = new CaseHearing
        {
            Id = Guid.NewGuid(),
            CaseId = activeCase.Id,
            HearingDate = new DateOnly(2026, 6, 20),
            IsDeleted = false
        };
        db.CaseHearings.AddRange(hearing1, hearing2);

        await db.SaveChangesAsync();

        // 1. Query cases by indexed Status + FilingDate (respecting soft-delete)
        var activeCases = await db.Cases.Where(c => c.Status == CaseStatus.Active).OrderBy(c => c.FilingDate).ToListAsync();
        activeCases.Should().HaveCount(1);
        activeCases[0].CaseNumber.Should().Be("C-101");

        // 2. Query clients by indexed NationalIdNumber
        var clientByNid = await db.Clients.FirstOrDefaultAsync(c => c.NationalIdNumber == "29001011234502");
        clientByNid.Should().NotBeNull();
        clientByNid!.FullName.Should().Be("Mahmoud Hassan");

        // 3. Query clients by indexed FullName
        var clientByName = await db.Clients.FirstOrDefaultAsync(c => c.FullName == "Ahmed Kamal");
        clientByName.Should().NotBeNull();

        // 4. Query companies by indexed Category and RegistrationNumber
        var capitalComps = await db.Companies.Where(c => c.Category == CompanyCategory.CapitalCompany).ToListAsync();
        capitalComps.Should().HaveCount(1);
        capitalComps[0].RegistrationNumber.Should().Be("CR-101");

        // 5. Query case hearings by indexed CaseId and HearingDate
        var hearings = await db.CaseHearings.Where(h => h.CaseId == activeCase.Id && h.HearingDate >= new DateOnly(2026, 5, 1)).ToListAsync();
        hearings.Should().HaveCount(2);
    }
}
