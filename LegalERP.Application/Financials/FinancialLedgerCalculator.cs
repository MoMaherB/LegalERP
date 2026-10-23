using System;
using System.Collections.Generic;
using System.Linq;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Financials;

public static class FinancialLedgerCalculator
{
    public const string CaseOwnerType = "Case";
    public const string CompanyOwnerType = "Company";

    public static decimal CalculateTotalCollected(IEnumerable<FeeTransaction>? transactions)
        => transactions?.Sum(t => t.Amount) ?? 0m;

    public static decimal CalculateRemainingBalance(decimal? agreedFee, decimal totalCollected)
        => agreedFee.HasValue ? agreedFee.Value - totalCollected : 0m;

    public static PaymentStatus CalculateStatus(decimal? agreedFee, decimal totalCollected)
    {
        if (agreedFee.HasValue && agreedFee.Value > 0m)
        {
            if (totalCollected >= agreedFee.Value) return PaymentStatus.Paid;
            if (totalCollected > 0m) return PaymentStatus.PartiallyPaid;
        }

        return PaymentStatus.Unpaid;
    }

    public static FeeTransactionDto ToTransactionDto(FeeTransaction transaction) =>
        new(
            transaction.Id,
            transaction.Amount,
            transaction.TransactionDate,
            transaction.ReceiptNumber,
            transaction.Notes
        );

    public static EntityFinancialsDto ToEntityFinancialsDto(decimal? agreedFee, IEnumerable<FeeTransaction>? transactions)
    {
        var orderedTransactions = transactions?
            .OrderBy(t => t.TransactionDate)
            .ToList() ?? new List<FeeTransaction>();

        var totalCollected = CalculateTotalCollected(orderedTransactions);
        var remainingBalance = CalculateRemainingBalance(agreedFee, totalCollected);
        var status = CalculateStatus(agreedFee, totalCollected);

        return new EntityFinancialsDto(
            agreedFee,
            totalCollected,
            remainingBalance,
            status,
            orderedTransactions.Select(ToTransactionDto).ToList()
        );
    }

    public static GlobalFinancialsDto BuildSummary(IEnumerable<Case> cases, IEnumerable<Company> companies)
    {
        var dto = new GlobalFinancialsDto();

        foreach (var c in cases)
        {
            var record = CreateRecord(c.Id, CaseOwnerType, c.Title, c.CaseNumber, c.AgreedFee, c.FeeTransactions);
            dto.TotalCaseAgreedFees += record.AgreedFee ?? 0m;
            dto.TotalCaseCollected += record.TotalCollected;
            dto.Records.Add(record);
        }

        foreach (var company in companies)
        {
            var record = CreateRecord(company.Id, CompanyOwnerType, company.CompanyName, ResolveCompanyReferenceNumber(company), company.AgreedFee, company.FeeTransactions);
            dto.TotalCompanyAgreedFees += record.AgreedFee ?? 0m;
            dto.TotalCompanyCollected += record.TotalCollected;
            dto.Records.Add(record);
        }

        dto.Records = dto.Records.OrderByDescending(r => r.AgreedFee).ToList();
        return dto;
    }

    public static string ResolveCompanyReferenceNumber(Company company)
    {
        if (!string.IsNullOrWhiteSpace(company.RegistrationNumber))
            return company.RegistrationNumber;
        if (!string.IsNullOrWhiteSpace(company.FileNumber))
            return company.FileNumber;
        return "N/A";
    }

    private static FinancialRecordDto CreateRecord(
        Guid ownerId,
        string ownerType,
        string ownerName,
        string referenceNumber,
        decimal? agreedFee,
        IEnumerable<FeeTransaction>? transactions)
    {
        var collected = CalculateTotalCollected(transactions);
        return new FinancialRecordDto
        {
            OwnerId = ownerId,
            OwnerType = ownerType,
            OwnerName = ownerName,
            ReferenceNumber = referenceNumber,
            AgreedFee = agreedFee,
            TotalCollected = collected,
            PaymentStatus = CalculateStatus(agreedFee, collected)
        };
    }
}
