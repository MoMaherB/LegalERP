using System;
using System.Collections.Generic;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Financials;

public class GlobalFinancialsDto
{
    public decimal TotalCaseAgreedFees { get; set; }
    public decimal TotalCaseCollected { get; set; }
    
    public decimal TotalCompanyAgreedFees { get; set; }
    public decimal TotalCompanyCollected { get; set; }
    
    public decimal FirmTotalAgreedFees => TotalCaseAgreedFees + TotalCompanyAgreedFees;
    public decimal FirmTotalCollected => TotalCaseCollected + TotalCompanyCollected;
    
    public List<FinancialRecordDto> Records { get; set; } = new();
}

public class FinancialRecordDto
{
    public Guid OwnerId { get; set; }
    public string OwnerType { get; set; } = string.Empty; // "Case" or "Company"
    public string OwnerName { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty; // CaseNumber or CR number
    
    public decimal? AgreedFee { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal RemainingBalance => (AgreedFee ?? 0) - TotalCollected;
    
    public PaymentStatus PaymentStatus { get; set; }
}
