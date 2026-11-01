using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Backups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin")]
public class BackupsController : ControllerBase
{
    private readonly IBackupArchiveService _backupService;
    private readonly IBackupJobCoordinator _jobCoordinator;

    public BackupsController(
        IBackupArchiveService backupService,
        IBackupJobCoordinator jobCoordinator)
    {
        _backupService = backupService;
        _jobCoordinator = jobCoordinator;
    }

    [HttpGet]
    public async Task<ActionResult<List<BackupRecordDto>>> GetHistory(CancellationToken ct)
    {
        var history = await _backupService.GetBackupHistoryAsync(25, ct);
        return Ok(history);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BackupStatusSummaryDto>> GetSummary(CancellationToken ct)
    {
        var summary = await _backupService.GetStatusSummaryAsync(ct);
        return Ok(summary);
    }

    [HttpGet("active")]
    public async Task<ActionResult<ActiveBackupStatusDto>> GetActiveBackup(CancellationToken ct)
    {
        var status = await _jobCoordinator.GetActiveBackupStatusAsync(ct);
        return Ok(status);
    }

    [HttpPost("export-archive")]
    public async Task<ActionResult<BackupStartResultDto>> ExportArchive(CancellationToken ct)
        => ToStartActionResult(await _jobCoordinator.StartArchiveBackupAsync(ct));

    [HttpPost("sync-developer-backup")]
    public async Task<ActionResult<BackupStartResultDto>> SyncDeveloperBackup(CancellationToken ct)
        => ToStartActionResult(await _jobCoordinator.StartDeveloperBackupAsync(ct));

    private ActionResult<BackupStartResultDto> ToStartActionResult(BackupStartResultDto result)
        => !result.Started ? Conflict(result) : Ok(result);

    [HttpPost("cancel")]
    public async Task<ActionResult<BackupCancelResultDto>> CancelActiveBackup(CancellationToken ct)
    {
        var result = await _jobCoordinator.CancelActiveBackupAsync(ct);
        return Ok(result);
    }

    [HttpGet("download-snapshot")]
    public async Task<IActionResult> DownloadDatabaseSnapshot(CancellationToken ct)
    {
        try
        {
            var (stream, fileName, length) = await _backupService.CreateDatabaseSnapshotAsync(ct);
            return File(stream, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database snapshot failed: {ex.Message}");
        }
    }
}
