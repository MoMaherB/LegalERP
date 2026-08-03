using System;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class CaseHearing : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }

    public DateOnly HearingDate { get; set; }
    public string? Purpose { get; set; }
    public string? JudgeDecision { get; set; }
    public string? PostponementReason { get; set; }
}
