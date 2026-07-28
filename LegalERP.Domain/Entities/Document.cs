using System;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class Document : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
}
