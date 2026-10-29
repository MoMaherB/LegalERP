using FluentAssertions;
using LegalERP.Application.Backups;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using Xunit;

namespace LegalERP.Tests.Backups;

public class BackupArchiveManifestBuilderTests
{
    [Theory]
    [InlineData(null, "بدون اسم")]
    [InlineData("   ", "بدون اسم")]
    [InlineData("///:::***", "ملف")]
    [InlineData("2026/105 : محكمة شمال * القاهرة", "2026-105 - محكمة شمال - القاهرة")]
    public void SanitizeName_ReplacesForbiddenCharactersAndHandlesBlankInputs(string? input, string expected)
    {
        BackupArchiveManifestBuilder.SanitizeName(input).Should().Be(expected);
    }

    [Fact]
    public void Build_IncludesAmendmentsBulkPartnerDocsAndCaseMemosWithTrueDocumentOwnerMetadata()
    {
        var companyId = Guid.NewGuid();
        var amendment1Id = Guid.NewGuid();
        var amendment2Id = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var memoId = Guid.NewGuid();
        var attorneyId = Guid.NewGuid();

        var incorpDoc = new Document { Id = Guid.NewGuid(), FileName = "incorp.pdf", StoredFileName = "s_incorp.pdf", OwnerType = DocumentOwnerTypes.Company, OwnerId = companyId };
        var partnerIdsDoc = new Document { Id = Guid.NewGuid(), FileName = "ids.pdf", StoredFileName = "s_ids.pdf", OwnerType = DocumentOwnerTypes.PartnerIds, OwnerId = companyId };
        var partnerAttDoc = new Document { Id = Guid.NewGuid(), FileName = "poas.pdf", StoredFileName = "s_poas.pdf", OwnerType = DocumentOwnerTypes.PartnerAttorneys, OwnerId = companyId };
        var amend1Doc = new Document { Id = Guid.NewGuid(), FileName = "a1.pdf", StoredFileName = "s_a1.pdf", OwnerType = DocumentOwnerTypes.Amendment, OwnerId = amendment1Id };
        var amend2Doc = new Document { Id = Guid.NewGuid(), FileName = "a2.docx", StoredFileName = "s_a2.docx", OwnerType = DocumentOwnerTypes.Amendment, OwnerId = amendment2Id };
        var compGeneralDoc = new Document { Id = Guid.NewGuid(), FileName = "ترخيص تجاري.pdf", StoredFileName = "s_lic.pdf", OwnerType = DocumentOwnerTypes.CompanyDoc, OwnerId = companyId };

        var company = new Company
        {
            Id = companyId,
            FileNumber = "101",
            CompanyName = "شركة النور / للتجارة",
            IncorporationDocument = incorpDoc,
            PartnersIdsDocument = partnerIdsDoc,
            PartnersAttorneysDocument = partnerAttDoc,
            Amendments =
            [
                new CompanyAmendment { Id = amendment2Id, CompanyId = companyId, SequenceNumber = 2, Title = "عقد التعديل الثاني", Document = amend2Doc },
                new CompanyAmendment { Id = amendment1Id, CompanyId = companyId, SequenceNumber = 1, Title = "عقد التعديل الأول", Document = amend1Doc }
            ]
        };

        var clientIdDoc = new Document { Id = Guid.NewGuid(), FileName = "nid.jpg", StoredFileName = "s_nid.jpg", OwnerType = DocumentOwnerTypes.Client, OwnerId = clientId };
        var client = new Client
        {
            Id = clientId,
            FileNumber = "C-50",
            FullName = "أحمد محمود",
            NationalIdDocument = clientIdDoc
        };

        var memoDoc = new Document { Id = Guid.NewGuid(), FileName = "memo.pdf", StoredFileName = "s_memo.pdf", OwnerType = DocumentOwnerTypes.CaseMemo, OwnerId = memoId };
        var caseGeneralDoc = new Document { Id = Guid.NewGuid(), FileName = "حافظة مستندات.pdf", StoredFileName = "s_casedoc.pdf", OwnerType = DocumentOwnerTypes.Case, OwnerId = caseId };
        var legalCase = new Case
        {
            Id = caseId,
            CaseNumber = "2026/88",
            CourtName = "محكمة جنوب",
            Memos =
            [
                new CaseMemo { Id = memoId, CaseId = caseId, Title = "مذكرة بالطلبات الختامية", Document = memoDoc }
            ]
        };

        var attorneyDoc = new Document { Id = Guid.NewGuid(), FileName = "poa.png", StoredFileName = "s_poa.png", OwnerType = DocumentOwnerTypes.Attorney, OwnerId = attorneyId };
        var attorney = new Attorney
        {
            Id = attorneyId,
            AttorneyNumber = "990/أ",
            Document = attorneyDoc
        };

        // Act
        var manifest = BackupArchiveManifestBuilder.Build(
            companies: [company],
            companyGeneralDocs: [compGeneralDoc],
            clients: [client],
            cases: [legalCase],
            caseGeneralDocs: [caseGeneralDoc],
            attorneys: [attorney]);

        // Assert
        var expectedCompDir = Path.Combine("companies", "101 - شركة النور - للتجارة");
        var expectedAmendDir = Path.Combine(expectedCompDir, "التعديلات");
        var expectedCompRecordsDir = Path.Combine(expectedCompDir, "سجلات وتراخيص الشركة");
        var expectedClientDir = Path.Combine("clients", "C-50 - أحمد محمود");
        var expectedCaseDir = Path.Combine("cases", "قضية 2026-88 - محكمة جنوب");
        var expectedMemosDir = Path.Combine(expectedCaseDir, "المذكرات القانونية");
        var expectedCaseDocsDir = Path.Combine(expectedCaseDir, "المستندات والمستخرجات");

        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(incorpDoc, expectedCompDir, "عقد التأسيس_101.pdf"));
        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(partnerIdsDoc, expectedCompDir, "بطاقات الشركاء_101.pdf"));
        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(partnerAttDoc, expectedCompDir, "توكيلات الشركاء_101.pdf"));

        // Amendments ordered by SequenceNumber (1 then 2) and preserving OwnerType = "amendment", OwnerId = amendmentId
        var amendEntries = manifest.Entries.Where(e => e.RelativeDirectory == expectedAmendDir).ToList();
        amendEntries.Should().HaveCount(2);
        amendEntries[0].FileName.Should().Be("عقد التعديل رقم 1.pdf");
        amendEntries[0].SourceDocument.OwnerType.Should().Be(DocumentOwnerTypes.Amendment);
        amendEntries[0].SourceDocument.OwnerId.Should().Be(amendment1Id);
        amendEntries[1].FileName.Should().Be("عقد التعديل رقم 2.docx");
        amendEntries[1].SourceDocument.OwnerType.Should().Be(DocumentOwnerTypes.Amendment);
        amendEntries[1].SourceDocument.OwnerId.Should().Be(amendment2Id);

        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(compGeneralDoc, expectedCompRecordsDir, "ترخيص تجاري.pdf"));
        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(clientIdDoc, expectedClientDir, "صورة الهوية_C-50.jpg"));

        // Case memo preserving OwnerType = "casememo", OwnerId = memoId
        var memoEntry = manifest.Entries.Single(e => e.RelativeDirectory == expectedMemosDir);
        memoEntry.FileName.Should().Be("مذكرة بالطلبات الختامية.pdf");
        memoEntry.SourceDocument.OwnerType.Should().Be(DocumentOwnerTypes.CaseMemo);
        memoEntry.SourceDocument.OwnerId.Should().Be(memoId);

        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(caseGeneralDoc, expectedCaseDocsDir, "حافظة مستندات.pdf"));
        manifest.Entries.Should().ContainEquivalentOf(new BackupArchiveEntry(attorneyDoc, "attornies", "توكيل_990-أ.png"));
    }

    [Fact]
    public void Build_WhenMultipleFilesHaveSameNameInSameFolder_DisambiguatesWithCounterInMemory()
    {
        var caseId = Guid.NewGuid();
        var doc1 = new Document { Id = Guid.NewGuid(), FileName = "m1.pdf", StoredFileName = "s1.pdf", OwnerType = DocumentOwnerTypes.CaseMemo, OwnerId = Guid.NewGuid() };
        var doc2 = new Document { Id = Guid.NewGuid(), FileName = "m2.pdf", StoredFileName = "s2.pdf", OwnerType = DocumentOwnerTypes.CaseMemo, OwnerId = Guid.NewGuid() };
        var doc3 = new Document { Id = Guid.NewGuid(), FileName = "m3.pdf", StoredFileName = "s3.pdf", OwnerType = DocumentOwnerTypes.CaseMemo, OwnerId = Guid.NewGuid() };

        var legalCase = new Case
        {
            Id = caseId,
            CaseNumber = "2026/99",
            Memos =
            [
                new CaseMemo { Id = doc1.OwnerId, CaseId = caseId, Title = "مذكرة دفاع", Document = doc1 },
                new CaseMemo { Id = doc2.OwnerId, CaseId = caseId, Title = "مذكرة دفاع", Document = doc2 },
                new CaseMemo { Id = doc3.OwnerId, CaseId = caseId, Title = "مذكرة دفاع", Document = doc3 }
            ]
        };

        var manifest = BackupArchiveManifestBuilder.Build(
            companies: [],
            companyGeneralDocs: [],
            clients: [],
            cases: [legalCase],
            caseGeneralDocs: [],
            attorneys: []);

        manifest.Entries.Select(e => e.FileName).Should().Equal(
            "مذكرة دفاع.pdf",
            "مذكرة دفاع (2).pdf",
            "مذكرة دفاع (3).pdf");
    }

    [Fact]
    public void Build_WhenEntitiesHaveZeroDocuments_OmitsEmptyDirectoriesFromManifest()
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            CompanyName = "شركة بدون مستندات",
            FileNumber = "999"
        };

        var client = new Client
        {
            Id = Guid.NewGuid(),
            FullName = "موكل بدون مستندات",
            FileNumber = "C-999"
        };

        var legalCase = new Case
        {
            Id = Guid.NewGuid(),
            CaseNumber = "2026/000",
            Title = "قضية بدون مستندات"
        };

        var attorney = new Attorney
        {
            Id = Guid.NewGuid(),
            AttorneyNumber = "000/بدون"
        };

        var manifest = BackupArchiveManifestBuilder.Build(
            companies: [company],
            companyGeneralDocs: [],
            clients: [client],
            cases: [legalCase],
            caseGeneralDocs: [],
            attorneys: [attorney]);

        manifest.Entries.Should().BeEmpty();
        manifest.Directories.Should().BeEmpty();
    }
}
