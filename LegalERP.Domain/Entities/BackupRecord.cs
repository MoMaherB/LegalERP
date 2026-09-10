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
}
