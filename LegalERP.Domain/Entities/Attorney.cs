using System;
using System.Collections.Generic;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class Attorney : BaseEntity
{
    public string AttorneyNumber { get; set; } = string.Empty;  // رقم التوكيل
    public string? Notes { get; set; }

    // The scanned attorney document (photo)
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }

    // Many-to-many: one attorney covers multiple clients
    public List<AttorneyClient> AttorneyClients { get; set; } = new();
}
