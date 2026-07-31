using System;
using System.Collections.Generic;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class Client : BaseEntity
{
    public string FullName { get; set; } = string.Empty;          // الاسم الكامل
    public string? FullNameEn { get; set; }                       // English name (optional)
    public string? NationalIdNumber { get; set; }                 // رقم الهوية
    public string? PhoneNumber { get; set; }                      // رقم الهاتف
    public string? Email { get; set; }                            // البريد الإلكتروني
    public string? Address { get; set; }                          // العنوان
    public string? Notes { get; set; }                            // ملاحظات

    // Document FKs
    public Guid? NationalIdDocumentId { get; set; }               // صورة الهوية
    public Document? NationalIdDocument { get; set; }
    public Guid? AttorneyDocumentId { get; set; }                 // صورة التوكيل / الوكالة
    public Document? AttorneyDocument { get; set; }

    // Navigation — all cases and companies linked to this client
    public List<CaseParty> CaseParties { get; set; } = new();
    public List<CompanyPartner> CompanyPartnerships { get; set; } = new();
}
