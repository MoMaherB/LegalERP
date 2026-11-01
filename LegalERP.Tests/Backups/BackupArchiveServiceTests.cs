using FluentAssertions;
using LegalERP.Application.Backups;
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

        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, new FakeRcloneRunner());

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

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_InvokesRcloneSyncToLawyerRemoteAndSetsDestinationPath()
    {
        var (db, _) = await CreateTestDbWithCompanyAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath,
                ["Backup:RcloneLawyerRemote"] = "gdrive:LegalERP_Test/Lawyer_Archive"
            })
            .Build();

        var fakeRclone = new FakeRcloneRunner();
        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, fakeRclone);

        // Act: null customTargetPath triggers standard production staging + rclone sync
        var result = await service.GenerateHumanReadableArchiveAsync(null);

        // Assert
        result.Success.Should().BeTrue();
        fakeRclone.LastSyncSource.Should().NotBeNullOrWhiteSpace();
        fakeRclone.LastSyncDestination.Should().Be("gdrive:LegalERP_Test/Lawyer_Archive");

        var backupRecord = await db.BackupRecords.OrderByDescending(r => r.CreatedAt).FirstAsync();
        backupRecord.Status.Should().Be(LegalERP.Domain.Enums.BackupStatus.Completed);
        backupRecord.DestinationPath.Should().Be("gdrive:LegalERP_Test/Lawyer_Archive");
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_WhenRcloneFails_SetsStatusFailedAndRecordsErrorMessage()
    {
        var (db, _) = await CreateTestDbWithCompanyAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath,
                ["Backup:RcloneLawyerRemote"] = "gdrive:LegalERP_Test/Lawyer_Archive"
            })
            .Build();

        var fakeRclone = new FakeRcloneRunner
        {
            SyncResult = new RcloneExecutionResult(false, 1, "", "Fatal: connection refused")
        };
        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, fakeRclone);

        // Act
        var result = await service.GenerateHumanReadableArchiveAsync(null);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Fatal: connection refused");

        var backupRecord = await db.BackupRecords.OrderByDescending(r => r.CreatedAt).FirstAsync();
        backupRecord.Status.Should().Be(LegalERP.Domain.Enums.BackupStatus.Failed);
        backupRecord.ErrorMessage.Should().Contain("Fatal: connection refused");
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_CleansStaleFilesAndOmitsEmptyDirectoriesFromStagingOnDisk()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("BackupArchiveTest_" + Guid.NewGuid())
            .Options;
        await using var db = new ApplicationDbContext(options);

        // Add an entity with 0 documents
        db.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "شركة خالية من المستندات" });
        await db.SaveChangesAsync();

        // Pre-create stale folders/files in the staging directory
        var staleDir = Path.Combine(_archiveOutputPath, "companies", "شركة محذوفة قديمة");
        Directory.CreateDirectory(staleDir);
        await File.WriteAllTextAsync(Path.Combine(staleDir, "old_doc.pdf"), "stale content");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath
            })
            .Build();

        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, new FakeRcloneRunner());

        // Act
        var result = await service.GenerateHumanReadableArchiveAsync(_archiveOutputPath);

        // Assert
        result.Success.Should().BeTrue();
        result.FileCount.Should().Be(0);

        // Stale folder was cleaned
        Directory.Exists(staleDir).Should().BeFalse();
        // Zero-document company folder was never created
        Directory.Exists(Path.Combine(_archiveOutputPath, "companies", "شركة خالية من المستندات")).Should().BeFalse();
        Directory.Exists(Path.Combine(_archiveOutputPath, "companies")).Should().BeFalse();
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_WithExistingRunningRecord_UpdatesExistingRecordInsteadOfCreatingDuplicate()
    {
        var (db, _) = await CreateTestDbWithCompanyAsync();

        var existingId = Guid.NewGuid();
        db.BackupRecords.Add(new BackupRecord
        {
            Id = existingId,
            BackupType = LegalERP.Domain.Enums.BackupType.GoogleDriveArchive,
            Status = LegalERP.Domain.Enums.BackupStatus.Running,
            CreatedAt = DateTime.UtcNow.AddSeconds(-5)
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath,
                ["Backup:RcloneLawyerRemote"] = "gdrive:LegalERP_Test/Lawyer_Archive"
            })
            .Build();

        var fakeRclone = new FakeRcloneRunner();
        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, fakeRclone);

        var result = await service.GenerateHumanReadableArchiveAsync(null, CancellationToken.None, existingId);

        result.Success.Should().BeTrue();
        var records = await db.BackupRecords.ToListAsync();
        records.Should().HaveCount(1);
        records[0].Id.Should().Be(existingId);
        records[0].Status.Should().Be(LegalERP.Domain.Enums.BackupStatus.Completed);
        records[0].DestinationPath.Should().Be("gdrive:LegalERP_Test/Lawyer_Archive");
        records[0].CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_WhenCancelledDuringRcloneSync_MarksRecordCancelledWithCompletionTime()
    {
        var (db, _) = await CreateTestDbWithCompanyAsync();

        var existingId = Guid.NewGuid();
        db.BackupRecords.Add(new BackupRecord
        {
            Id = existingId,
            BackupType = LegalERP.Domain.Enums.BackupType.GoogleDriveArchive,
            Status = LegalERP.Domain.Enums.BackupStatus.Running,
            CreatedAt = DateTime.UtcNow.AddSeconds(-5)
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath,
                ["Backup:RcloneLawyerRemote"] = "gdrive:LegalERP_Test/Lawyer_Archive"
            })
            .Build();

        using var cts = new CancellationTokenSource();
        var fakeRclone = new FakeRcloneRunner
        {
            OnSyncCalled = () => cts.Cancel()
        };
        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, fakeRclone);

        var result = await service.GenerateHumanReadableArchiveAsync(null, cts.Token, existingId);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cancelled");
        var record = await db.BackupRecords.SingleAsync(r => r.Id == existingId);
        record.Status.Should().Be(LegalERP.Domain.Enums.BackupStatus.Cancelled);
        record.CompletedAt.Should().NotBeNull();
        record.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_WhileRcloneSyncInFlight_PersistsStagedFileCountAndSizeOnRunningRecord()
    {
        var (db, _) = await CreateTestDbWithCompanyAsync();

        var existingId = Guid.NewGuid();
        db.BackupRecords.Add(new BackupRecord
        {
            Id = existingId,
            BackupType = LegalERP.Domain.Enums.BackupType.GoogleDriveArchive,
            Status = LegalERP.Domain.Enums.BackupStatus.Running,
            CreatedAt = DateTime.UtcNow.AddSeconds(-5)
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath,
                ["Backup:RcloneLawyerRemote"] = "gdrive:LegalERP_Test/Lawyer_Archive"
            })
            .Build();

        int? inFlightFileCount = null;
        long? inFlightTotalBytes = null;

        var fakeRclone = new FakeRcloneRunner
        {
            OnSyncCalled = () =>
            {
                var runningRecord = db.BackupRecords.AsNoTracking().Single(r => r.Id == existingId);
                inFlightFileCount = runningRecord.FileCount;
                inFlightTotalBytes = runningRecord.TotalSizeBytes;
            }
        };
        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, fakeRclone);

        var result = await service.GenerateHumanReadableArchiveAsync(null, CancellationToken.None, existingId);

        result.Success.Should().BeTrue();
        inFlightFileCount.Should().Be(1);
        inFlightTotalBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateHumanReadableArchiveAsync_WhenRcloneRetryInflatesTotalBytes_NormalizesProgressToActualArchiveSize()
    {
        var (db, _) = await CreateTestDbWithCompanyAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = _storageBasePath,
                ["Backup:RcloneLawyerRemote"] = "gdrive:LegalERP_Test/Lawyer_Archive"
            })
            .Build();

        BackupProgressSnapshotDto? reportedSnapshot = null;
        var fakeRclone = new FakeRcloneRunner
        {
            SimulatedProgress = new BackupProgressSnapshotDto(
                Percentage: 65,
                TransferredBytes: 15_639_175,
                TotalBytes: 24_040_636,
                TransferredText: "14.915 MiB",
                TotalText: "22.927 MiB",
                SpeedText: "398.406 KiB/s",
                EtaText: "20s")
        };
        var service = new BackupArchiveService(db, config, NullLogger<BackupArchiveService>.Instance, fakeRclone);

        var result = await service.GenerateHumanReadableArchiveAsync(
            null,
            CancellationToken.None,
            null,
            s => reportedSnapshot = s);

        result.Success.Should().BeTrue();
        reportedSnapshot.Should().NotBeNull();
        reportedSnapshot!.TotalBytes.Should().Be(result.TotalSizeBytes);
        reportedSnapshot.TransferredBytes.Should().BeLessThanOrEqualTo(result.TotalSizeBytes);
    }

    private async Task<(ApplicationDbContext Db, Company Company)> CreateTestDbWithCompanyAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("BackupArchiveTest_" + Guid.NewGuid())
            .Options;
        var db = new ApplicationDbContext(options);

        var companyId = Guid.NewGuid();
        var incorpDoc = CreateStoredDocument(DocumentOwnerTypes.Company, companyId, "incorp.pdf", "s_incorp.pdf", "incorp-bytes");
        db.Documents.Add(incorpDoc);
        var comp = new Company
        {
            Id = companyId,
            CompanyName = "شركة النصر",
            IncorporationDocumentId = incorpDoc.Id,
            IncorporationDocument = incorpDoc
        };
        db.Companies.Add(comp);
        await db.SaveChangesAsync();

        return (db, comp);
    }

    private class FakeRcloneRunner : LegalERP.Application.Backups.IRcloneRunner
    {
        public string? LastSyncSource { get; private set; }
        public string? LastSyncDestination { get; private set; }
        public RcloneExecutionResult SyncResult { get; set; } = new(true, 0, "OK", "");
        public Action? OnSyncCalled { get; set; }
        public BackupProgressSnapshotDto? SimulatedProgress { get; set; }

        public Task<RcloneExecutionResult> SyncDirectoryAsync(
            string localDirectoryPath,
            string remoteDestination,
            CancellationToken ct = default,
            Action<BackupProgressSnapshotDto>? onProgress = null)
        {
            LastSyncSource = localDirectoryPath;
            LastSyncDestination = remoteDestination;
            OnSyncCalled?.Invoke();
            ct.ThrowIfCancellationRequested();
            if (SimulatedProgress != null && onProgress != null)
            {
                onProgress(SimulatedProgress);
            }
            return Task.FromResult(SyncResult);
        }

        public Task<RcloneExecutionResult> CopyFileAsync(
            string localFilePath,
            string remoteDestinationDirectory,
            CancellationToken ct = default,
            Action<BackupProgressSnapshotDto>? onProgress = null)
        {
            return Task.FromResult(new RcloneExecutionResult(true, 0, "OK", ""));
        }
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
