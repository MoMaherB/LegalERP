using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class CompanyPartner : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }         // Navigation to parent Company
    public Guid? ClientId { get; set; }           // FK to central Client record
    public Client? Client { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? NationalIdNumber { get; set; }
    public decimal? OwnershipPercentage { get; set; }
    public Guid? NationalIdDocumentId { get; set; }
    public Document? NationalIdDocument { get; set; }
}
