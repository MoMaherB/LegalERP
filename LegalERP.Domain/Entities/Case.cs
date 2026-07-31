using System;
using LegalERP.Domain.Common;
using LegalERP.Domain.Enums;

namespace LegalERP.Domain.Entities;

public class Case : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty; // رقم القضية
    public string Title { get; set; } = string.Empty;      // عنوان / موضوع القضية
    public CaseType CaseType { get; set; }
    public CaseStatus Status { get; set; } = CaseStatus.Active;
    public CaseOutcome? Outcome { get; set; }              // ربح / خسارة / صلح
    public DateOnly? FilingDate { get; set; }              // تاريخ القيد / الرفع
    public string? CourtName { get; set; }                 // المحكمة
    public string? JudgeName { get; set; }                 // اسم القاضي / الدائرة
    public string? Notes { get; set; }                     // ملاحظات

    public List<CaseParty> Parties { get; set; } = new();
}
