using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Application.Backups;

public interface IBackupArchiveService
{
    Task<BackupResultDto> GenerateHumanReadableArchiveAsync(string? customTargetPath = null, CancellationToken ct = default);
    Task<(Stream FileStream, string FileName, long FileSizeBytes)> CreateDatabaseSnapshotAsync(CancellationToken ct = default);
    Task<List<BackupRecordDto>> GetBackupHistoryAsync(int take = 20, CancellationToken ct = default);
    Task<BackupStatusSummaryDto> GetStatusSummaryAsync(CancellationToken ct = default);
}
