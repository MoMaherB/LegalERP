using System;
using LegalERP.Domain.Common;
using LegalERP.Domain.Enums;

namespace LegalERP.Domain.Entities;

public class CaseParty : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }               // Navigation to parent Case
    public Guid? ClientId { get; set; }           // FK to central Client record
    public Client? Client { get; set; }
    public string FullName { get; set; } = string.Empty;
    public PartyRole Role { get; set; }           // Defendant (المتهم) / Victim (المجني عليه)
    public bool IsOurClient { get; set; }         // true = Client (Green), false = Opponent (Red)
    public string? NationalIdNumber { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }

    public Guid? DocumentId { get; set; }
    public Document? Document { get; set; }
}
