using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LegalERP.Application.Storage;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream content, string originalFileName, string ownerType, Guid ownerId, CancellationToken ct = default);
    Task<Stream?> GetFileAsync(string ownerType, Guid ownerId, string storedFileName, CancellationToken ct = default);
    Task DeleteFileAsync(string ownerType, Guid ownerId, string storedFileName, CancellationToken ct = default);
}
