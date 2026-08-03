using System;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class FeeTransaction : BaseEntity
{
    public Guid? CaseId { get; set; }
    public Case? Case { get; set; }

    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public decimal Amount { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
}
