using FluentAssertions;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using LegalERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LegalERP.Tests.Backups;

public class BackupArchiveServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _storageBasePath;
    private readonly string _archiveOutputPath;

    public BackupArchiveServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LegalERP_BackupTests_" + Guid.NewGuid().ToString("N"));
        _storageBasePath = Path.Combine(_tempRoot, "uploads");
        _archiveOutputPath = Path.Combine(_tempRoot, "archive_out");
        Directory.CreateDirectory(_storageBasePath);
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_ExportsAmendmentsBulkPartnerDocsAndCaseMemosFromTrueOwnerPaths()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("BackupArchiveTest_" + Guid.NewGuid())
            .Options;
        await using var db = new ApplicationDbContext(options);

        var companyId = Guid.NewGuid();
        var amendmentId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var memo1Id = Guid.NewGuid();
        var memo2Id = Guid.NewGuid();

        var partnerIdsDoc = CreateStoredDocument(DocumentOwnerTypes.PartnerIds, companyId, "pids.pdf", "stored_pids.pdf", "partner-ids-bytes");
        var partnerAttDoc = CreateStoredDocument(DocumentOwnerTypes.PartnerAttorneys, companyId, "patt.pdf", "stored_patt.pdf", "partner-att-bytes");
        var amendDoc = CreateStoredDocument(DocumentOwnerTypes.Amendment, amendmentId, "amend1.pdf", "stored_amend.pdf", "amendment-bytes");
        var memo1Doc = CreateStoredDocument(DocumentOwnerTypes.CaseMemo, memo1Id, "m1.pdf", "stored_m1.pdf", "memo-1-bytes");
        var memo2Doc = CreateStoredDocument(DocumentOwnerTypes.CaseMemo, memo2Id, "m2.pdf", "stored_m2.pdf", "memo-2-bytes");

        db.Documents.AddRange(partnerIdsDoc, partnerAttDoc, amendDoc, memo1Doc, memo2Doc);

        var company = new Company
        {
            Id = companyId,
            FileNumber = "200",
            CompanyName = "شركة الأمل",
            HasManyPartners = true,
            PartnersIdsDocumentId = partnerIdsDoc.Id,
            PartnersIdsDocument = partnerIdsDoc,
            PartnersAttorneysDocumentId = partnerAttDoc.Id,
            PartnersAttorneysDocument = partnerAttDoc,
            Amendments =
            [
                new CompanyAmendment
                {
                    Id = amendmentId,
                    CompanyId = companyId,
                    SequenceNumber = 1,
                    Title = "عقد التعديل الأول",
                    DocumentId = amendDoc.Id,
                    Document = amendDoc
                }
            ]
        };
        db.Companies.Add(company);

        var legalCase = new Case
        {
            Id = caseId,
            CaseNumber = "555/2026",
            Title = "قضية مدنية",
            CourtName = "محكمة الجيزة",
            Memos =
            [
                new CaseMemo { Id = memo1Id, CaseId = caseId, Title = "مذكرة دفاع", DocumentId = memo1Doc.Id, Document = memo1Doc },
                new CaseMemo { Id = memo2Id, CaseId = caseId, Title = "مذكرة دفاع", DocumentId = memo2Doc.Id, Document = memo2Doc }
            ]
        };
        db.Cases.Add(legalCase);

        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath
            })
            .Build();

        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance);

        // Act
        var result = await service.GenerateHumanReadableArchiveAsync(_archiveOutputPath);

        // Assert
        result.Success.Should().BeTrue();
        result.FileCount.Should().Be(5);

        var compDir = Path.Combine(_archiveOutputPath, "companies", "200 - شركة الأمل");
        File.Exists(Path.Combine(compDir, "بطاقات الشركاء_200.pdf")).Should().BeTrue();
        File.Exists(Path.Combine(compDir, "توكيلات الشركاء_200.pdf")).Should().BeTrue();
        File.Exists(Path.Combine(compDir, "التعديلات", "عقد التعديل رقم 1.pdf")).Should().BeTrue();

        var memosDir = Path.Combine(_archiveOutputPath, "cases", "قضية 555-2026 - محكمة الجيزة", "المذكرات القانونية");
        File.Exists(Path.Combine(memosDir, "مذكرة دفاع.pdf")).Should().BeTrue();
        File.Exists(Path.Combine(memosDir, "مذكرة دفاع (2).pdf")).Should().BeTrue();
    }

    private Document CreateStoredDocument(string ownerType, Guid ownerId, string originalName, string storedName, string content)
    {
        var dir = Path.Combine(_storageBasePath, ownerType.ToLowerInvariant(), ownerId.ToString());
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, storedName);
        File.WriteAllText(fullPath, content);

        return new Document
        {
            Id = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            FileName = originalName,
            StoredFileName = storedName,
            ContentType = "application/pdf",
            FileSizeBytes = new FileInfo(fullPath).Length
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp folder
        }
    }
}
