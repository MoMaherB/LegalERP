using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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

    public string ComputeETag()
    {
        using var sha = SHA256.Create();
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant($"{TotalCaseAgreedFees}:{TotalCaseCollected}:{TotalCompanyAgreedFees}:{TotalCompanyCollected}:{Records.Count}:"));
        foreach (var r in Records)
        {
            sb.Append(FormattableString.Invariant($"{r.OwnerId}:{r.AgreedFee}:{r.TotalCollected}:{(int)r.PaymentStatus};"));
        }
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return $"\"{Convert.ToHexString(hashBytes).ToLowerInvariant()}\"";
    }
}

public class FinancialRecordDto
{
    public Guid OwnerId { get; set; }
    public string OwnerType { get; set; } = string.Empty; // "Case" or "Company"
    public string OwnerName { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty; // CaseNumber or CR number
    
    public decimal? AgreedFee { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal RemainingBalance => FinancialLedgerCalculator.CalculateRemainingBalance(AgreedFee, TotalCollected);
    
    public PaymentStatus PaymentStatus { get; set; }
}
