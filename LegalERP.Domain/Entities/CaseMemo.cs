using System;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class CaseMemo : BaseEntity
{
    public Guid CaseId { get; set; }
    public Case? Case { get; set; }
    public string Title { get; set; } = string.Empty;       // عنوان المذكرة
    public string? Content { get; set; }                   // نص / موضوع المذكرة
    public DateOnly MemoDate { get; set; }                 // تاريخ المذكرة
    public Guid? DocumentId { get; set; }                  // المستند المرفق
    public Document? Document { get; set; }
}
