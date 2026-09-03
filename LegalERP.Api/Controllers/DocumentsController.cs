using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Storage;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // All endpoints require authentication
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _storage;

    public DocumentsController(ApplicationDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
    [HttpPost("upload")]
    [RequestSizeLimit(100 * 1024 * 1024)] // Allow large uploads up to 100MB, we'll compress/handle later if needed
    public async Task<ActionResult<Guid>> Upload(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        [FromQuery] string? title,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx" };
        if (Array.IndexOf(allowedExtensions, ext) < 0)
            return BadRequest("Invalid file type. Only PDF, Word documents, and images are allowed.");

        using var stream = file.OpenReadStream();
        
        var storedFileName = await _storage.SaveFileAsync(stream, file.FileName, ownerType, ownerId, ct);

        // Use the custom title if provided, otherwise fallback to the original filename
        var displayName = !string.IsNullOrWhiteSpace(title)
            ? title + ext  // keep the extension so download works correctly
            : file.FileName;

        var document = new Document
        {
            FileName = displayName,
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

    [AllowAnonymous]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();

        var stream = await _storage.GetFileAsync(doc.OwnerType, doc.OwnerId, doc.StoredFileName, ct);
        if (stream == null) return NotFound("File missing on disk.");

        return File(stream, doc.ContentType, doc.FileName);
    }

    /// <summary>
    /// Serves the file inline in the browser (Content-Disposition: inline) for preview/viewing.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:guid}/view")]
    public async Task<IActionResult> View(Guid id, CancellationToken ct)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc == null) return NotFound();

        var stream = await _storage.GetFileAsync(doc.OwnerType, doc.OwnerId, doc.StoredFileName, ct);
        if (stream == null) return NotFound("File missing on disk.");

        // Return without filename = Content-Disposition: inline (browser renders it)
        return File(stream, doc.ContentType);
    }

    /// <summary>
    /// Serves documents and profile pictures by file path or stored file name.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("download")]
    public async Task<IActionResult> DownloadByPath([FromQuery] string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return BadRequest("Path is required.");

        var fileName = Path.GetFileName(path);

        // 1. Check if it's a user profile picture
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ProfilePicturePath == fileName, ct);
        if (user != null)
        {
            var stream = await _storage.GetFileAsync("users", user.Id, fileName, ct);
            if (stream != null)
            {
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
                return File(stream, contentType, fileName);
            }
        }

        // 2. Check if it's a stored document in Documents table
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.StoredFileName == fileName, ct);
        if (doc != null)
        {
            var stream = await _storage.GetFileAsync(doc.OwnerType, doc.OwnerId, doc.StoredFileName, ct);
            if (stream != null)
            {
                return File(stream, doc.ContentType, doc.FileName);
            }
        }

        // 3. Fallback: Search uploads folder on disk for matching filename
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (Directory.Exists(baseDir))
        {
            var matchedFiles = Directory.GetFiles(baseDir, fileName, SearchOption.AllDirectories);
            if (matchedFiles.Length > 0)
            {
                var filePath = matchedFiles[0];
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".pdf" => "application/pdf",
                    _ => "application/octet-stream"
                };
                var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return File(stream, contentType, fileName);
            }
        }

        return NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<List<LegalERP.Application.Companies.DocumentDto>>> GetByOwner(
        [FromQuery] string ownerType,
        [FromQuery] Guid ownerId,
        CancellationToken ct)
    {
        var docs = await _db.Documents
            .Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId && !d.IsDeleted)
            .Select(d => new LegalERP.Application.Companies.DocumentDto(
                d.Id,
                d.FileName,
                d.StoredFileName,
                d.ContentType,
                d.FileSizeBytes))
            .ToListAsync(ct);
            
        return Ok(docs);
    }

    [Authorize(Roles = "SuperAdmin,Admin,Editor")]
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
