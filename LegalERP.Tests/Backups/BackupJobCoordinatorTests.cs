using FluentAssertions;
using LegalERP.Application.Backups;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using LegalERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LegalERP.Tests.Backups;

public class BackupJobCoordinatorTests
{
    [Fact]
    public async Task StartArchiveBackupAsync_WhenBackupAlreadyRunning_RejectsSecondStartAndRunsArchiveOnlyOnce()
    {
        var dbName = "CoordinatorSingleFlight_" + Guid.NewGuid();
        var tcs = new TaskCompletionSource<BackupResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fakeArchiveService = new GateArchiveService(tcs.Task);

        using var provider = BuildServiceProvider(dbName, fakeArchiveService);
        var coordinator = provider.GetRequiredService<IBackupJobCoordinator>();

        // First call starts the background job and returns immediately
        var first = await coordinator.StartArchiveBackupAsync();
        first.Started.Should().BeTrue();
        first.BackupId.Should().NotBeNull();

        // Active status while running
        var activeWhileRunning = await coordinator.GetActiveBackupStatusAsync();
        activeWhileRunning.IsRunning.Should().BeTrue();
        activeWhileRunning.BackupId.Should().Be(first.BackupId);

        // Second concurrent call while first is still running -> rejected
        var second = await coordinator.StartArchiveBackupAsync();
        second.Started.Should().BeFalse();
        second.BackupId.Should().Be(first.BackupId);

        // Let the background job finish
        tcs.SetResult(new BackupResultDto(true, "Done", 16, 12_410_000, "gdrive:LegalERP_Test/Lawyer_Archive"));

        // Wait briefly for background task to release lock
        await WaitUntilNotRunningAsync(coordinator);
        fakeArchiveService.CallCount.Should().Be(1);

        var activeAfter = await coordinator.GetActiveBackupStatusAsync();
        activeAfter.IsRunning.Should().BeFalse();

        using (var verifyScope = provider.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var completedRecord = await db.BackupRecords.FindAsync(first.BackupId);
            completedRecord.Should().NotBeNull();
            completedRecord!.Status.Should().Be(BackupStatus.Completed);
            completedRecord.FileCount.Should().Be(16);
            completedRecord.TotalSizeBytes.Should().Be(12_410_000);
            completedRecord.DestinationPath.Should().Be("gdrive:LegalERP_Test/Lawyer_Archive");
            completedRecord.CompletedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ReconcileStaleRunningBackupsAsync_MarksStaleRunningRecordsAsFailedWithInterruptedMessage()
    {
        var dbName = "CoordinatorReconcile_" + Guid.NewGuid();
        var tcs = new TaskCompletionSource<BackupResultDto>();
        using var provider = BuildServiceProvider(dbName, new GateArchiveService(tcs.Task));

        Guid staleId1;
        Guid staleId2;
        Guid completedId;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r1 = new BackupRecord
            {
                Id = Guid.NewGuid(),
                BackupType = BackupType.GoogleDriveArchive,
                Status = BackupStatus.Running,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15)
            };
            var r2 = new BackupRecord
            {
                Id = Guid.NewGuid(),
                BackupType = BackupType.GoogleDriveArchive,
                Status = BackupStatus.Running,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            };
            var r3 = new BackupRecord
            {
                Id = Guid.NewGuid(),
                BackupType = BackupType.GoogleDriveArchive,
                Status = BackupStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-2)
            };
            staleId1 = r1.Id;
            staleId2 = r2.Id;
            completedId = r3.Id;
            db.BackupRecords.AddRange(r1, r2, r3);
            await db.SaveChangesAsync();
        }

        var coordinator = provider.GetRequiredService<IBackupJobCoordinator>();
        var reconciledCount = await coordinator.ReconcileStaleRunningBackupsAsync();

        reconciledCount.Should().Be(2);

        using (var verifyScope = provider.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r1 = await db.BackupRecords.FindAsync(staleId1);
            var r2 = await db.BackupRecords.FindAsync(staleId2);
            var r3 = await db.BackupRecords.FindAsync(completedId);

            r1!.Status.Should().Be(BackupStatus.Failed);
            r1.ErrorMessage.Should().Contain("Interrupted");
            r1.CompletedAt.Should().NotBeNull();

            r2!.Status.Should().Be(BackupStatus.Failed);
            r2.ErrorMessage.Should().Contain("Interrupted");
            r2.CompletedAt.Should().NotBeNull();

            r3!.Status.Should().Be(BackupStatus.Completed);
        }
    }

    [Fact]
    public async Task GetActiveBackupStatusAsync_WhileArchiveJobReportsProgress_ExposesLatestProgressSnapshotAndClearsOnFinish()
    {
        var dbName = "CoordinatorProgress_" + Guid.NewGuid();
        var tcs = new TaskCompletionSource<BackupResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fakeArchiveService = new GateArchiveService(tcs.Task)
        {
            OnEntered = () => enteredTcs.TrySetResult()
        };

        using var provider = BuildServiceProvider(dbName, fakeArchiveService);
        var coordinator = provider.GetRequiredService<IBackupJobCoordinator>();

        var start = await coordinator.StartArchiveBackupAsync();
        start.Started.Should().BeTrue();

        await enteredTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var sampleProgress = new BackupProgressSnapshotDto(
            Percentage: 62,
            TransferredBytes: 7_700_000,
            TotalBytes: 12_410_000,
            TransferredText: "7.34 MiB",
            TotalText: "11.83 MiB",
            SpeedText: "1.45 MiB/s",
            EtaText: "3s");

        fakeArchiveService.ReportProgress(sampleProgress);

        var statusWhileRunning = await coordinator.GetActiveBackupStatusAsync();
        statusWhileRunning.IsRunning.Should().BeTrue();
        statusWhileRunning.Progress.Should().BeEquivalentTo(sampleProgress);

        tcs.SetResult(new BackupResultDto(true, "Done", 16, 12_410_000, "gdrive:LegalERP_Test/Lawyer_Archive"));
        await WaitUntilNotRunningAsync(coordinator);

        var statusAfterFinish = await coordinator.GetActiveBackupStatusAsync();
        statusAfterFinish.IsRunning.Should().BeFalse();
        statusAfterFinish.Progress.Should().BeNull();
    }

    [Fact]
    public async Task CancelActiveBackupAsync_WhenBackupIsRunning_StopsJobMarksRecordCancelledAndAllowsNewStart()
    {
        var dbName = "CoordinatorCancel_" + Guid.NewGuid();
        var tcs = new TaskCompletionSource<BackupResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fakeArchiveService = new GateArchiveService(tcs.Task)
        {
            OnEntered = () => enteredTcs.TrySetResult()
        };

        using var provider = BuildServiceProvider(dbName, fakeArchiveService);
        var coordinator = provider.GetRequiredService<IBackupJobCoordinator>();

        // 1. Start first backup
        var firstStart = await coordinator.StartArchiveBackupAsync();
        firstStart.Started.Should().BeTrue();
        await enteredTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // 2. Cancel running backup
        var cancelResult = await coordinator.CancelActiveBackupAsync();
        cancelResult.Cancelled.Should().BeTrue();
        cancelResult.BackupId.Should().Be(firstStart.BackupId);

        // 3. Active status should be idle immediately after CancelActiveBackupAsync completes
        var statusAfterCancel = await coordinator.GetActiveBackupStatusAsync();
        statusAfterCancel.IsRunning.Should().BeFalse();

        // 4. First BackupRecord should be marked Cancelled with CompletedAt
        using (var verifyScope = provider.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cancelledRecord = await db.BackupRecords.FindAsync(firstStart.BackupId);
            cancelledRecord.Should().NotBeNull();
            cancelledRecord!.Status.Should().Be(BackupStatus.Cancelled);
            cancelledRecord.CompletedAt.Should().NotBeNull();
        }

        // 5. A new backup can now start and complete normally
        var secondTcs = new TaskCompletionSource<BackupResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        secondTcs.SetResult(new BackupResultDto(true, "Done", 16, 12_410_000, "gdrive:LegalERP_Test/Lawyer_Archive"));
        fakeArchiveService.SetNextGate(secondTcs.Task);

        var secondStart = await coordinator.StartArchiveBackupAsync();
        secondStart.Started.Should().BeTrue();
        secondStart.BackupId.Should().NotBe(firstStart.BackupId!.Value);

        await WaitUntilNotRunningAsync(coordinator);
    }

    [Fact]
    public async Task CancelActiveBackupAsync_WhenNoBackupIsRunning_ReturnsCancelledFalse()
    {
        var dbName = "CoordinatorCancelIdle_" + Guid.NewGuid();
        var tcs = new TaskCompletionSource<BackupResultDto>();
        using var provider = BuildServiceProvider(dbName, new GateArchiveService(tcs.Task));
        var coordinator = provider.GetRequiredService<IBackupJobCoordinator>();

        var result = await coordinator.CancelActiveBackupAsync();

        result.Cancelled.Should().BeFalse();
        result.BackupId.Should().BeNull();
    }

    private static ServiceProvider BuildServiceProvider(string dbName, GateArchiveService archiveService)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<IBackupArchiveService>(sp =>
        {
            archiveService.ScopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            return archiveService;
        });
        services.AddSingleton<IBackupJobCoordinator>(sp =>
            new BackupJobCoordinator(
                sp.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<BackupJobCoordinator>.Instance));
        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilNotRunningAsync(IBackupJobCoordinator coordinator)
    {
        for (int i = 0; i < 50; i++)
        {
            var status = await coordinator.GetActiveBackupStatusAsync();
            if (!status.IsRunning) return;
            await Task.Delay(20);
        }
    }

    private class GateArchiveService : IBackupArchiveService
    {
        private Task<BackupResultDto> _gate;
        private int _callCount;
        private Action<BackupProgressSnapshotDto>? _lastProgressCallback;
        public int CallCount => _callCount;
        public IServiceScopeFactory? ScopeFactory { get; set; }
        public Action? OnEntered { get; set; }

        public GateArchiveService(Task<BackupResultDto> gate)
        {
            _gate = gate;
        }

        public void SetNextGate(Task<BackupResultDto> nextGate)
        {
            _gate = nextGate;
        }

        public void ReportProgress(BackupProgressSnapshotDto snapshot)
        {
            _lastProgressCallback?.Invoke(snapshot);
        }

        public async Task<BackupResultDto> GenerateHumanReadableArchiveAsync(
            string? customTargetPath = null,
            CancellationToken ct = default,
            Guid? existingRecordId = null,
            Action<BackupProgressSnapshotDto>? onProgress = null)
        {
            Interlocked.Increment(ref _callCount);
            _lastProgressCallback = onProgress;
            OnEntered?.Invoke();
            var result = await _gate.WaitAsync(ct);

            if (existingRecordId.HasValue && ScopeFactory != null)
            {
                using var scope = ScopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var record = await db.BackupRecords.FirstOrDefaultAsync(r => r.Id == existingRecordId.Value, ct);
                if (record != null)
                {
                    if (result.Success)
                    {
                        record.MarkCompleted(result.FileCount, result.TotalSizeBytes, result.OutputPath);
                    }
                    else
                    {
                        record.MarkFailed(result.Message, result.FileCount, result.TotalSizeBytes, result.OutputPath);
                    }
                    await db.SaveChangesAsync(ct);
                }
            }

            return result;
        }

        public Task<(Stream FileStream, string FileName, long FileSizeBytes)> CreateDatabaseSnapshotAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<List<BackupRecordDto>> GetBackupHistoryAsync(int take = 20, CancellationToken ct = default)
            => Task.FromResult(new List<BackupRecordDto>());

        public Task<BackupStatusSummaryDto> GetStatusSummaryAsync(CancellationToken ct = default)
            => Task.FromResult(new BackupStatusSummaryDto(null, 0, 0, null, 0, 0));
    }
}
