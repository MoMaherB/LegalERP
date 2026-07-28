using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Storage;

namespace LegalERP.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    // For development, we store in Api/wwwroot/uploads
    // In production, this path will be read from configuration (e.g., /var/legalerp/storage)
    private readonly string _baseStoragePath;

    public LocalFileStorageService()
    {
        // Defaulting to local dev folder for now
        _baseStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    }

    public async Task<string> SaveFileAsync(Stream content, string originalFileName, string ownerType, Guid ownerId, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid()}{extension}";
        
        var directoryPath = Path.Combine(_baseStoragePath, ownerType.ToLower(), ownerId.ToString());
        Directory.CreateDirectory(directoryPath);

        var filePath = Path.Combine(directoryPath, storedFileName);

        // TODO: We could add image compression here if the file is an image > 10MB
        // For now, we simply save the stream to disk.
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        return storedFileName;
    }

    public Task<Stream?> GetFileAsync(string ownerType, Guid ownerId, string storedFileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_baseStoragePath, ownerType.ToLower(), ownerId.ToString(), storedFileName);
        
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        // Open read-only stream
        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string ownerType, Guid ownerId, string storedFileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_baseStoragePath, ownerType.ToLower(), ownerId.ToString(), storedFileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        return Task.CompletedTask;
    }
}
