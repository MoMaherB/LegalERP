using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Backups;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LegalERP.Infrastructure.Services;

public class BackupArchiveService : IBackupArchiveService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<BackupArchiveService> _logger;
    private readonly IRcloneRunner _rcloneRunner;
    private readonly string _storageBasePath;
    private readonly Func<CancellationToken, Task<string>> _databaseDumpGenerator;

    public BackupArchiveService(
        ApplicationDbContext db,
        IConfiguration config,
        ILogger<BackupArchiveService> logger,
        IRcloneRunner rcloneRunner,
        Func<CancellationToken, Task<string>>? databaseDumpGenerator = null)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _rcloneRunner = rcloneRunner;
        _databaseDumpGenerator = databaseDumpGenerator ?? GenerateDatabaseDumpFileAsync;

        // Base directory where uploads currently reside
        var configuredPath = _config["Storage:BasePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageBasePath = Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(Directory.GetCurrentDirectory(), configuredPath);
        }
        else
        {
            var apiPath = Path.Combine(Directory.GetCurrentDirectory(), "LegalERP.Api", "wwwroot", "uploads");
            if (Directory.Exists(apiPath))
            {
                _storageBasePath = apiPath;
            }
            else
            {
                _storageBasePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            }
        }
    }

    public async Task<BackupResultDto> GenerateHumanReadableArchiveAsync(
        string? customTargetPath = null,
        CancellationToken ct = default,
        Guid? existingRecordId = null,
        Action<BackupProgressSnapshotDto>? onProgress = null)
    {
        var targetBase = !string.IsNullOrWhiteSpace(customTargetPath)
            ? customTargetPath
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "exports", "legal_archive");

        var record = await GetOrCreateRunningRecordAsync(
            BackupType.GoogleDriveArchive,
            existingRecordId,
            targetBase,
            ct);

        int totalFilesCopied = 0;
        long totalBytesCopied = 0;

        try
        {
            // Clean any stale staging files and folders before rebuilding
            if (Directory.Exists(targetBase))
            {
                try
                {
                    Directory.Delete(targetBase, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete directory {TargetBase} recursively; cleaning files individually.", targetBase);
                    foreach (var file in Directory.GetFiles(targetBase, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(file); }
                        catch (Exception fileEx)
                        {
                            _logger.LogWarning(fileEx, "Failed to delete file {File} during cleanup.", file);
                        }
                    }
                    foreach (var subDir in Directory.GetDirectories(targetBase, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                    {
                        try { Directory.Delete(subDir, recursive: false); }
                        catch (Exception dirEx)
                        {
                            _logger.LogWarning(dirEx, "Failed to delete directory {Dir} during cleanup.", subDir);
                        }
                    }
                }
            }

            var companies = await _db.Companies
                .Include(c => c.IncorporationDocument)
                .Include(c => c.PartnersIdsDocument)
                .Include(c => c.PartnersAttorneysDocument)
                .Include(c => c.Amendments).ThenInclude(a => a.Document)
                .ToListAsync(ct);

            var companyDocs = await _db.Documents
                .Where(d => d.OwnerType == DocumentOwnerTypes.CompanyDoc && !d.IsDeleted)
                .ToListAsync(ct);

            var clients = await _db.Clients
                .Include(c => c.NationalIdDocument)
                .ToListAsync(ct);

            var cases = await _db.Cases
                .Include(c => c.Memos).ThenInclude(m => m.Document)
                .ToListAsync(ct);

            var caseDocs = await _db.Documents
                .Where(d => d.OwnerType == DocumentOwnerTypes.Case && !d.IsDeleted)
                .ToListAsync(ct);

            var attorneys = await _db.Attorneys
                .Include(a => a.Document)
                .ToListAsync(ct);

            var manifest = BackupArchiveManifestBuilder.Build(
                companies,
                companyDocs,
                clients,
                cases,
                caseDocs,
                attorneys);

            // Only create targetBase if there is at least one entry/directory to export
            if (manifest.Entries.Count > 0)
            {
                Directory.CreateDirectory(targetBase);

                foreach (var relativeDir in manifest.Directories)
                {
                    ct.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(Path.Combine(targetBase, relativeDir));
                }

                foreach (var entry in manifest.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var targetDir = Path.Combine(targetBase, entry.RelativeDirectory);

                    if (LinkOrCopyDocument(entry.SourceDocument, targetDir, entry.FileName, out var size))
                    {
                        totalFilesCopied++;
                        totalBytesCopied += size;
                    }
                }
            }

            // Persist staged file count and total size immediately so the Running row reflects actual archive metrics
            record.FileCount = totalFilesCopied;
            record.TotalSizeBytes = totalBytesCopied;
            await _db.SaveChangesAsync(ct);

            // Sync to Google Drive via rclone if configured and not an explicit local-only override
            var isLocalOnlyOverride = !string.IsNullOrWhiteSpace(customTargetPath);
            var remoteOptions = BackupRemoteOptionsResolver.Resolve(_config);
            var lawyerRemote = remoteOptions.LawyerRemote;

            if (!isLocalOnlyOverride && !string.IsNullOrWhiteSpace(lawyerRemote))
            {
                if (manifest.Entries.Count == 0)
                {
                    _logger.LogWarning("Legal archive has 0 files. Skipping rclone sync to prevent purging remote archive at {Remote}.", lawyerRemote);
                }
                else
                {
                    // Ensure directory exists so rclone has a valid source directory
                    if (!Directory.Exists(targetBase))
                    {
                        Directory.CreateDirectory(targetBase);
                    }

                    var normalizedProgress = CreateNormalizedProgressCallback(onProgress, totalBytesCopied);

                    _logger.LogInformation("Syncing lawyer archive from {Source} to {Remote} via rclone...", targetBase, lawyerRemote);
                    var syncResult = await _rcloneRunner.SyncDirectoryAsync(targetBase, lawyerRemote, ct, normalizedProgress);
                    if (!syncResult.Success)
                    {
                        var errMsg = $"rclone sync to {lawyerRemote} failed ({syncResult.ExitCode}): {syncResult.StandardError}";
                        _logger.LogError(errMsg);

                        record.MarkFailed(errMsg, totalFilesCopied, totalBytesCopied, lawyerRemote);
                        await _db.SaveChangesAsync(ct);

                        return new BackupResultDto(false, errMsg, totalFilesCopied, totalBytesCopied, null);
                    }

                    record.DestinationPath = lawyerRemote;
                }
            }

            // Mark completed
            record.MarkCompleted(totalFilesCopied, totalBytesCopied);
            await _db.SaveChangesAsync(ct);

            var finalDestination = record.DestinationPath;
            return new BackupResultDto(true, "Legal archive generated successfully.", totalFilesCopied, totalBytesCopied, finalDestination);
        }
        catch (OperationCanceledException)
        {
            return await HandleBackupCancelledAsync(record, totalFilesCopied, totalBytesCopied, null);
        }
        catch (Exception ex)
        {
            return await HandleBackupFailedAsync(record, ex, "Failed to generate archive", totalFilesCopied, totalBytesCopied, null);
        }
    }

    public async Task<BackupResultDto> SyncDeveloperSystemBackupAsync(
        CancellationToken ct = default,
        Guid? existingRecordId = null,
        Action<BackupProgressSnapshotDto>? onProgress = null)
    {
        var remoteOptions = BackupRemoteOptionsResolver.Resolve(_config);
        var devRemote = remoteOptions.DeveloperRemote?.TrimEnd('/');

        var record = await GetOrCreateRunningRecordAsync(
            BackupType.DatabaseSnapshot,
            existingRecordId,
            devRemote ?? "Google Drive Developer Backup",
            ct);

        int totalFilesCount = 0;
        long totalBytesCount = 0;
        string? tempDumpFile = null;

        try
        {
            if (string.IsNullOrWhiteSpace(devRemote))
            {
                const string missingRemoteMsg = "Developer Google Drive remote (Backup:RcloneDeveloperRemote) is not configured.";
                record.MarkFailed(missingRemoteMsg);
                await _db.SaveChangesAsync(ct);
                return new BackupResultDto(false, missingRemoteMsg, 0, 0, null);
            }

            ct.ThrowIfCancellationRequested();
            tempDumpFile = await _databaseDumpGenerator(ct);
            var dumpInfo = new FileInfo(tempDumpFile);
            long dumpBytes = dumpInfo.Exists ? dumpInfo.Length : 0;

            int rawUploadsCount = 0;
            long rawUploadsBytes = 0;
            if (Directory.Exists(_storageBasePath))
            {
                foreach (var file in Directory.EnumerateFiles(_storageBasePath, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();
                    rawUploadsCount++;
                    rawUploadsBytes += new FileInfo(file).Length;
                }
            }
            else
            {
                Directory.CreateDirectory(_storageBasePath);
            }

            totalFilesCount = 1 + rawUploadsCount;
            totalBytesCount = dumpBytes + rawUploadsBytes;

            record.FileCount = totalFilesCount;
            record.TotalSizeBytes = totalBytesCount;
            record.DestinationPath = devRemote;
            await _db.SaveChangesAsync(ct);

            var dumpProgress = CreateNormalizedProgressCallback(onProgress, dumpBytes);
            var copyResult = await _rcloneRunner.CopyFileAsync(tempDumpFile, $"{devRemote}/Database", ct, dumpProgress);
            if (!copyResult.Success)
            {
                var errMsg = $"rclone copy of database dump to {devRemote}/Database failed ({copyResult.ExitCode}): {copyResult.StandardError}";
                _logger.LogError(errMsg);
                record.MarkFailed(errMsg, totalFilesCount, totalBytesCount, devRemote);
                await _db.SaveChangesAsync(ct);
                return new BackupResultDto(false, errMsg, totalFilesCount, totalBytesCount, null);
            }

            var uploadsProgress = CreateNormalizedProgressCallback(onProgress, rawUploadsBytes);
            var syncResult = await _rcloneRunner.SyncDirectoryAsync(_storageBasePath, $"{devRemote}/uploads", ct, uploadsProgress);
            if (!syncResult.Success)
            {
                var errMsg = $"rclone sync of uploads to {devRemote}/uploads failed ({syncResult.ExitCode}): {syncResult.StandardError}";
                _logger.LogError(errMsg);
                record.MarkFailed(errMsg, totalFilesCount, totalBytesCount, devRemote);
                await _db.SaveChangesAsync(ct);
                return new BackupResultDto(false, errMsg, totalFilesCount, totalBytesCount, null);
            }

            record.MarkCompleted(totalFilesCount, totalBytesCount, devRemote);
            await _db.SaveChangesAsync(ct);

            return new BackupResultDto(true, "Developer system backup synced successfully.", totalFilesCount, totalBytesCount, devRemote);
        }
        catch (OperationCanceledException)
        {
            return await HandleBackupCancelledAsync(record, totalFilesCount, totalBytesCount, devRemote);
        }
        catch (Exception ex)
        {
            return await HandleBackupFailedAsync(record, ex, "Failed to sync developer system backup", totalFilesCount, totalBytesCount, devRemote);
        }
        finally
        {
            TryDeleteTempFile(tempDumpFile);
        }
    }

    public async Task<(Stream FileStream, string FileName, long FileSizeBytes)> CreateDatabaseSnapshotAsync(CancellationToken ct = default)
    {
        var tempFile = await _databaseDumpGenerator(ct);
        var fileInfo = new FileInfo(tempFile);
        var fileStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);

        var record = new BackupRecord
        {
            BackupType = BackupType.DatabaseSnapshot,
            Status = BackupStatus.Completed,
            FileCount = 1,
            TotalSizeBytes = fileInfo.Length,
            DestinationPath = "Direct Stream",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _db.BackupRecords.Add(record);
        await _db.SaveChangesAsync(ct);

        return (fileStream, Path.GetFileName(tempFile), fileInfo.Length);
    }

    private async Task<BackupRecord> GetOrCreateRunningRecordAsync(
        BackupType backupType,
        Guid? existingRecordId,
        string? defaultDestinationPath,
        CancellationToken ct)
    {
        BackupRecord? record = null;
        if (existingRecordId.HasValue)
        {
            record = await _db.BackupRecords.FirstOrDefaultAsync(r => r.Id == existingRecordId.Value, ct);
        }

        if (record == null)
        {
            record = new BackupRecord
            {
                Id = existingRecordId ?? Guid.NewGuid(),
                BackupType = backupType,
                Status = BackupStatus.Running,
                DestinationPath = defaultDestinationPath,
                CreatedAt = DateTime.UtcNow
            };
            _db.BackupRecords.Add(record);
            await _db.SaveChangesAsync(ct);
        }
        else if (string.IsNullOrWhiteSpace(record.DestinationPath) && !string.IsNullOrWhiteSpace(defaultDestinationPath))
        {
            record.DestinationPath = defaultDestinationPath;
            await _db.SaveChangesAsync(ct);
        }

        return record;
    }

    private async Task<string> GenerateDatabaseDumpFileAsync(CancellationToken ct)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"legalerp_db_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dump");
        var pgDumpPath = FindPgDumpExecutable();

        if (string.IsNullOrWhiteSpace(pgDumpPath) || !File.Exists(pgDumpPath))
        {
            throw new InvalidOperationException("pg_dump executable not found on the system. Please ensure PostgreSQL client tools are installed.");
        }

        var connString = _config.GetConnectionString("DefaultConnection") ?? "";
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connString);

        var startInfo = new ProcessStartInfo
        {
            FileName = pgDumpPath,
            Arguments = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} -F c -f \"{tempFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(builder.Password))
        {
            startInfo.EnvironmentVariables["PGPASSWORD"] = builder.Password;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill errors
            }
            TryDeleteTempFile(tempFile);
            throw;
        }

        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            TryDeleteTempFile(tempFile);
            throw new Exception($"pg_dump failed (exit code {process.ExitCode}): {error}");
        }

        return tempFile;
    }

    private async Task<BackupResultDto> HandleBackupCancelledAsync(
        BackupRecord record,
        int fileCount,
        long totalBytes,
        string? destinationPath)
    {
        _logger.LogInformation("Backup job {BackupId} ({BackupType}) was cancelled.", record.Id, record.BackupType);
        record.MarkCancelled(fileCount, totalBytes, destinationPath: destinationPath);
        await _db.SaveChangesAsync(CancellationToken.None);
        return new BackupResultDto(false, "Backup job was cancelled.", fileCount, totalBytes, null);
    }

    private async Task<BackupResultDto> HandleBackupFailedAsync(
        BackupRecord record,
        Exception ex,
        string errorPrefix,
        int fileCount,
        long totalBytes,
        string? destinationPath)
    {
        _logger.LogError(ex, "{Prefix} for backup {BackupId}.", errorPrefix, record.Id);
        record.MarkFailed(ex.Message, fileCount, totalBytes, destinationPath);
        await _db.SaveChangesAsync(CancellationToken.None);
        return new BackupResultDto(false, $"{errorPrefix}: {ex.Message}", fileCount, totalBytes, null);
    }

    private static Action<BackupProgressSnapshotDto>? CreateNormalizedProgressCallback(
        Action<BackupProgressSnapshotDto>? onProgress,
        long expectedStepBytes)
    {
        if (onProgress == null)
        {
            return null;
        }

        return snapshot => onProgress(NormalizeProgressSnapshot(snapshot, expectedStepBytes));
    }

    private void TryDeleteTempFile(string? tempFilePath)
    {
        if (string.IsNullOrWhiteSpace(tempFilePath) || !File.Exists(tempFilePath))
        {
            return;
        }

        try
        {
            File.Delete(tempFilePath);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx, "Failed to delete temporary database dump {TempFile}", tempFilePath);
        }
    }

    public async Task<List<BackupRecordDto>> GetBackupHistoryAsync(int take = 20, CancellationToken ct = default)
    {
        return await _db.BackupRecords
            .OrderByDescending(b => b.CreatedAt)
            .Take(take)
            .Select(b => new BackupRecordDto(
                b.Id,
                b.BackupType,
                b.Status,
                b.FileCount,
                b.TotalSizeBytes,
                b.DestinationPath,
                b.ErrorMessage,
                b.CreatedAt,
                b.CompletedAt
            ))
            .ToListAsync(ct);
    }

    public async Task<BackupStatusSummaryDto> GetStatusSummaryAsync(CancellationToken ct = default)
    {
        var lastArchive = await _db.BackupRecords
            .Where(b => b.BackupType == BackupType.GoogleDriveArchive && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var lastDb = await _db.BackupRecords
            .Where(b => b.BackupType == BackupType.DatabaseSnapshot && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var totalRuns = await _db.BackupRecords.CountAsync(ct);

        return new BackupStatusSummaryDto(
            lastArchive?.CompletedAt ?? lastArchive?.CreatedAt,
            lastArchive?.FileCount ?? 0,
            lastArchive?.TotalSizeBytes ?? 0,
            lastDb?.CompletedAt ?? lastDb?.CreatedAt,
            lastDb?.TotalSizeBytes ?? 0,
            totalRuns
        );
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    private static BackupProgressSnapshotDto NormalizeProgressSnapshot(
        BackupProgressSnapshotDto snapshot,
        long archiveTotalBytes)
    {
        if (archiveTotalBytes <= 0 || snapshot.TotalBytes <= archiveTotalBytes)
        {
            return snapshot;
        }

        // When rclone retries a rate-limited/failed upload, it adds the retried file's bytes to TotalBytes.
        // Normalize against the true archive size based on remaining bytes (TotalBytes - TransferredBytes).
        var remainingBytes = Math.Clamp(snapshot.TotalBytes - snapshot.TransferredBytes, 0L, archiveTotalBytes);
        var effectiveTransferred = archiveTotalBytes - remainingBytes;
        var percentage = (int)Math.Clamp(Math.Round((double)effectiveTransferred * 100d / archiveTotalBytes), 0, 100);

        return snapshot with
        {
            Percentage = percentage,
            TransferredBytes = effectiveTransferred,
            TotalBytes = archiveTotalBytes,
            TransferredText = FormatBinaryBytes(effectiveTransferred),
            TotalText = FormatBinaryBytes(archiveTotalBytes)
        };
    }

    private static string FormatBinaryBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        const double kib = 1024d;
        const double mib = 1024d * 1024d;
        const double gib = 1024d * 1024d * 1024d;

        if (bytes >= gib)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / gib:F3} GiB");
        }
        if (bytes >= mib)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / mib:F3} MiB");
        }
        if (bytes >= kib)
        {
            return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / kib:F3} KiB");
        }
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes} B");
    }

    private bool LinkOrCopyDocument(Document doc, string targetDir, string desiredFileName, out long fileSize)
    {
        fileSize = 0;
        var ownerTypeSegment = (doc.OwnerType ?? string.Empty).ToLowerInvariant();
        var sourcePath = Path.Combine(_storageBasePath, ownerTypeSegment, doc.OwnerId.ToString(), doc.StoredFileName);

        if (!File.Exists(sourcePath))
        {
            // Fallback check in flat uploads
            sourcePath = Path.Combine(_storageBasePath, doc.StoredFileName);
            if (!File.Exists(sourcePath)) return false;
        }

        var fileInfo = new FileInfo(sourcePath);
        fileSize = fileInfo.Length;

        var targetPath = Path.Combine(targetDir, desiredFileName);

        try
        {
            // Try hardlink first (takes 0 extra disk space)
            if (File.Exists(targetPath)) File.Delete(targetPath);
            CreateHardLinkSafe(targetPath, sourcePath);
            return true;
        }
        catch
        {
            // Fallback to copy if hardlinks fail across volume boundaries
            try
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy file from {Source} to {Target}", sourcePath, targetPath);
                return false;
            }
        }
    }

    private static void CreateHardLinkSafe(string newFileName, string existingFileName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!CreateHardLinkWindows(newFileName, existingFileName, IntPtr.Zero))
            {
                File.Copy(existingFileName, newFileName, true);
            }
        }
        else
        {
            // Unix / Linux link()
            if (link(existingFileName, newFileName) != 0)
            {
                File.Copy(existingFileName, newFileName, true);
            }
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkWindows(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);

    private static string? FindPgDumpExecutable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var paths = new[]
            {
                @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
            };

            foreach (var p in paths)
            {
                if (File.Exists(p)) return p;
            }
        }
        else
        {
            var unixPaths = new[] { "/usr/bin/pg_dump", "/usr/local/bin/pg_dump" };
            foreach (var p in unixPaths)
            {
                if (File.Exists(p)) return p;
            }
        }

        return "pg_dump";
    }
}
