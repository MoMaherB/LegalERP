using System;
using LegalERP.Domain.Common;
using LegalERP.Domain.Enums;

namespace LegalERP.Domain.Entities;

public class BackupRecord : BaseEntity
{
    public BackupType BackupType { get; set; }
    public BackupStatus Status { get; set; }
    public int FileCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public string? DestinationPath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }

    public void MarkCompleted(int fileCount, long totalSizeBytes, string? destinationPath = null)
    {
        ApplyFinalState(BackupStatus.Completed, null, fileCount, totalSizeBytes, destinationPath);
    }

    public void MarkFailed(string errorMessage, int? fileCount = null, long? totalSizeBytes = null, string? destinationPath = null)
    {
        ApplyFinalState(BackupStatus.Failed, errorMessage, fileCount, totalSizeBytes, destinationPath);
    }

    public void MarkCancelled(int? fileCount = null, long? totalSizeBytes = null, string? destinationPath = null)
    {
        ApplyFinalState(BackupStatus.Cancelled, null, fileCount, totalSizeBytes, destinationPath);
    }

    private void ApplyFinalState(
        BackupStatus status,
        string? errorMessage,
        int? fileCount,
        long? totalSizeBytes,
        string? destinationPath)
    {
        Status = status;
        ErrorMessage = errorMessage;
        if (fileCount.HasValue)
        {
            FileCount = fileCount.Value;
        }
        if (totalSizeBytes.HasValue)
        {
            TotalSizeBytes = totalSizeBytes.Value;
        }
        if (!string.IsNullOrWhiteSpace(destinationPath))
        {
            DestinationPath = destinationPath;
        }
        CompletedAt = DateTime.UtcNow;
    }
}
