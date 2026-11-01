using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Backups;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalERP.Infrastructure.Services;

public class BackupJobCoordinator : IBackupJobCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupJobCoordinator> _logger;
    private readonly SemaphoreSlim _singleFlightLock = new(1, 1);

    private sealed record ActiveJobState(
        Guid BackupId,
        BackupType BackupType,
        DateTime StartedAt,
        CancellationTokenSource Cts,
        TaskCompletionSource CompletionTcs,
        BackupProgressSnapshotDto? Progress = null);
    private ActiveJobState? _activeJob;

    public BackupJobCoordinator(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupJobCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<BackupStartResultDto> StartArchiveBackupAsync(CancellationToken ct = default)
    {
        if (!await _singleFlightLock.WaitAsync(0, ct))
        {
            var current = _activeJob;
            return new BackupStartResultDto(
                Started: false,
                BackupId: current?.BackupId,
                Message: "A backup job is already running.");
        }

        Guid recordId = Guid.NewGuid();
        DateTime startedAt = DateTime.UtcNow;
        var jobCts = new CancellationTokenSource();
        var completionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var record = new BackupRecord
                {
                    Id = recordId,
                    BackupType = BackupType.GoogleDriveArchive,
                    Status = BackupStatus.Running,
                    CreatedAt = startedAt
                };
                db.BackupRecords.Add(record);
                await db.SaveChangesAsync(ct);
            }

            _activeJob = new ActiveJobState(recordId, BackupType.GoogleDriveArchive, startedAt, jobCts, completionTcs);

            _ = Task.Run(() => ExecuteArchiveJobInBackgroundAsync(recordId, jobCts, completionTcs));

            return new BackupStartResultDto(
                Started: true,
                BackupId: recordId,
                Message: "Backup job started in the background.");
        }
        catch
        {
            _activeJob = null;
            jobCts.Dispose();
            completionTcs.TrySetResult();
            _singleFlightLock.Release();
            throw;
        }
    }

    public async Task<BackupCancelResultDto> CancelActiveBackupAsync(CancellationToken ct = default)
    {
        var current = _activeJob;
        if (current == null)
        {
            return new BackupCancelResultDto(
                Cancelled: false,
                BackupId: null,
                Message: "No backup job is currently running.");
        }

        try
        {
            current.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Job already completed and disposed its CTS
        }

        await current.CompletionTcs.Task.WaitAsync(ct);

        return new BackupCancelResultDto(
            Cancelled: true,
            BackupId: current.BackupId,
            Message: "Backup job cancelled.");
    }

    public Task<ActiveBackupStatusDto> GetActiveBackupStatusAsync(CancellationToken ct = default)
    {
        var current = _activeJob;
        return Task.FromResult(new ActiveBackupStatusDto(
            IsRunning: current != null,
            BackupId: current?.BackupId,
            BackupType: current?.BackupType,
            StartedAt: current?.StartedAt,
            Progress: current?.Progress));
    }

    public async Task<int> ReconcileStaleRunningBackupsAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activeId = _activeJob?.BackupId;
        var staleRecords = await db.BackupRecords
            .Where(r => r.Status == BackupStatus.Running && r.Id != activeId)
            .ToListAsync(ct);

        if (staleRecords.Count == 0)
        {
            return 0;
        }

        foreach (var stale in staleRecords)
        {
            stale.MarkFailed("Interrupted: application restarted or backup job was terminated unexpectedly.");
        }

        await db.SaveChangesAsync(ct);
        _logger.LogWarning("Reconciled {Count} stale Running backup record(s) to Failed.", staleRecords.Count);
        return staleRecords.Count;
    }

    private async Task ExecuteArchiveJobInBackgroundAsync(
        Guid recordId,
        CancellationTokenSource jobCts,
        TaskCompletionSource completionTcs)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var archiveService = scope.ServiceProvider.GetRequiredService<IBackupArchiveService>();
            await archiveService.GenerateHumanReadableArchiveAsync(
                customTargetPath: null,
                ct: jobCts.Token,
                existingRecordId: recordId,
                onProgress: snapshot =>
                {
                    var current = _activeJob;
                    if (current != null && current.BackupId == recordId)
                    {
                        _activeJob = current with { Progress = snapshot };
                    }
                });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background archive backup job {BackupId} was cancelled.", recordId);
            await EnsureRecordMarkedCancelledAsync(recordId);
        }
        catch (Exception ex)
        {
            if (jobCts.IsCancellationRequested)
            {
                _logger.LogInformation("Background archive backup job {BackupId} stopped after cancellation request.", recordId);
                await EnsureRecordMarkedCancelledAsync(recordId);
            }
            else
            {
                _logger.LogError(ex, "Background archive backup job {BackupId} failed with an unhandled exception.", recordId);
                try
                {
                    using var errorScope = _scopeFactory.CreateScope();
                    var db = errorScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var record = await db.BackupRecords.FirstOrDefaultAsync(r => r.Id == recordId);
                    if (record != null && record.Status == BackupStatus.Running)
                    {
                        record.MarkFailed(ex.Message);
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to update backup record {BackupId} status after background failure.", recordId);
                }
            }
        }
        finally
        {
            if (jobCts.IsCancellationRequested)
            {
                await EnsureRecordMarkedCancelledAsync(recordId);
            }

            _activeJob = null;
            jobCts.Dispose();
            _singleFlightLock.Release();
            completionTcs.TrySetResult();
        }
    }

    private async Task EnsureRecordMarkedCancelledAsync(Guid recordId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.BackupRecords.FirstOrDefaultAsync(r => r.Id == recordId);
            if (record != null && record.Status != BackupStatus.Cancelled)
            {
                record.MarkCancelled();
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark backup record {BackupId} as Cancelled.", recordId);
        }
    }
}
