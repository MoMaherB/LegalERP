using System;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Backups;

public record BackupRecordDto(
    Guid Id,
    BackupType BackupType,
    BackupStatus Status,
    int FileCount,
    long TotalSizeBytes,
    string? DestinationPath,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record BackupResultDto(
    bool Success,
    string Message,
    int FileCount,
    long TotalSizeBytes,
    string? OutputPath
);

public record BackupStatusSummaryDto(
    DateTime? LastArchiveDate,
    int LastArchiveFileCount,
    long LastArchiveSizeBytes,
    DateTime? LastDatabaseSnapshotDate,
    long LastDatabaseSnapshotSizeBytes,
    int TotalBackupsRun
);

public record BackupStartResultDto(
    bool Started,
    Guid? BackupId,
    string Message
);

public record BackupCancelResultDto(
    bool Cancelled,
    Guid? BackupId,
    string Message
);

public record BackupProgressSnapshotDto(
    int Percentage,
    long TransferredBytes,
    long TotalBytes,
    string TransferredText,
    string TotalText,
    string SpeedText,
    string EtaText
);

public record ActiveBackupStatusDto(
    bool IsRunning,
    Guid? BackupId,
    BackupType? BackupType,
    DateTime? StartedAt,
    BackupProgressSnapshotDto? Progress = null
);
