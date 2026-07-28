using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Storage;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;

    public DocumentsController(ApplicationDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // Allow large uploads up to 100MB, we'll compress/handle later if needed
    public async Task<ActionResult<Guid>> Upload(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        if (Array.IndexOf(allowedExtensions, ext) < 0)
            return BadRequest("Invalid file type. Only PDF and images are allowed.");

        using var stream = file.OpenReadStream();
        
        var storedFileName = await _storage.SaveFileAsync(stream, file.FileName, ownerType, ownerId, ct);

        var document = new Document
        {
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            OwnerType = ownerType,
            OwnerId = ownerId
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        return Ok(document.Id);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();

        var stream = await _storage.GetFileAsync(doc.OwnerType, doc.OwnerId, doc.StoredFileName, ct);
        if (stream == null) return NotFound("File missing on disk.");

        return File(stream, doc.ContentType, doc.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();

        // Soft delete from DB
        doc.IsDeleted = true;
        doc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Optionally delete from disk, but usually with soft-delete we keep the file.
        // await _storage.DeleteFileAsync(doc.OwnerType, doc.OwnerId, doc.StoredFileName, ct);

        return NoContent();
    }
}
