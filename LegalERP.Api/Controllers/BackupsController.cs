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

    public BackupsController(IBackupArchiveService backupService)
    {
        _backupService = backupService;
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

    [HttpPost("export-archive")]
    public async Task<ActionResult<BackupResultDto>> ExportArchive(CancellationToken ct)
    {
        var result = await _backupService.GenerateHumanReadableArchiveAsync(null, ct);
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
