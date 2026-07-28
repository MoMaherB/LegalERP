using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class CompanyAmendment : BaseEntity
{
    public Guid CompanyId { get; set; }
    public int SequenceNumber { get; set; }     // 1st, 2nd, 3rd amendment...
    public string? Title { get; set; }
    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }
    public DateOnly? AmendmentDate { get; set; }
}
