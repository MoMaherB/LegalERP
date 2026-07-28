using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegalERP.Domain.Common;
using LegalERP.Domain.Enums;

namespace LegalERP.Domain.Entities;

public class Company : BaseEntity
{
    public CompanyCategory Category { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyNameEn { get; set; }   // optional English equivalent, for fuzzy search (TR-2.2)
    public string? TradeName { get; set; }       // السمة التجارية
    public DateOnly? EstablishmentDate { get; set; } // تاريخ التأسيس
    public string? RegistrationNumber { get; set; }  // رقم السجل التجاري
    public string? Address { get; set; }             // العنوان

    // FK to a Document (Documents module comes later — stored as a plain
    // nullable Guid for now, no navigation property yet, to keep this
    // module self-contained until Documents is built).
    public Guid? IncorporationDocumentId { get; set; } // عقد التأسيس

    // Navigation collections — populated once EF relationships are configured
    // in the Infrastructure layer (next steps).
    public List<CompanyAmendment> Amendments { get; set; } = new();
    public List<CompanyPartner> Partners { get; set; } = new();
}
