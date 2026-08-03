using System;
using System.Collections.Generic;
using LegalERP.Domain.Enums;

namespace LegalERP.Application.Financials;

public record EntityFinancialsDto(
    decimal? AgreedFee,
    decimal TotalCollected,
    decimal RemainingBalance,
    PaymentStatus PaymentStatus,
    List<FeeTransactionDto> Transactions
);

public record FeeTransactionDto(
    Guid Id,
    decimal Amount,
    DateOnly TransactionDate,
    string? ReceiptNumber,
    string? Notes
);

public class AddFeeTransactionDto
{
    public decimal Amount { get; set; }
    public DateOnly TransactionDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
}

public record UpdateAgreedFeeDto(
    decimal? AgreedFee
);
