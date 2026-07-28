using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.ComponentModel.DataAnnotations.Schema;

namespace LegalERP.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Not mapped to the database for now — concurrency handling is deferred
    // to a dedicated pre-production task (see CLAUDE.md TR-1.4 / Section 14).
    // The property stays here so the design is easy to revisit later without
    // touching every entity again.
    [NotMapped]
    public uint RowVersion { get; set; }
}