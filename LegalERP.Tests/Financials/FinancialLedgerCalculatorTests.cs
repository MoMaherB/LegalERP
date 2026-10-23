using FluentAssertions;
using LegalERP.Application.Financials;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Xunit;

namespace LegalERP.Tests.Financials;

public class FinancialLedgerCalculatorTests
{
    [Fact]
    public void ToEntityFinancialsDto_WhenAgreedFeeIsNull_ReturnsZeroRemainingBalanceAndUnpaid()
    {
        var transactions = new List<FeeTransaction>
        {
            new() { Id = Guid.NewGuid(), Amount = 500m, TransactionDate = new DateOnly(2026, 3, 10), ReceiptNumber = "R-1" }
        };

        var dto = FinancialLedgerCalculator.ToEntityFinancialsDto(agreedFee: null, transactions);

        dto.AgreedFee.Should().BeNull();
        dto.TotalCollected.Should().Be(500m);
        dto.RemainingBalance.Should().Be(0m);
        dto.PaymentStatus.Should().Be(PaymentStatus.Unpaid);
        dto.Transactions.Should().ContainSingle(t => t.ReceiptNumber == "R-1" && t.Amount == 500m);
    }

    [Fact]
    public void CalculateRemainingBalance_WhenCollectedExceedsAgreedFee_ReturnsZeroAndDoesNotGoNegative()
    {
        // User symptom: 50,000 agreed and 60,000 collected must result in 0 remaining balance, not -10,000
        var remaining = FinancialLedgerCalculator.CalculateRemainingBalance(agreedFee: 50000m, totalCollected: 60000m);
        remaining.Should().Be(0m);
    }

    [Theory]
    [InlineData(0, 0, 0, PaymentStatus.Unpaid)]
    [InlineData(1000, 0, 1000, PaymentStatus.Unpaid)]
    [InlineData(1000, 400, 600, PaymentStatus.PartiallyPaid)]
    [InlineData(1000, 1000, 0, PaymentStatus.Paid)]
    [InlineData(50000, 60000, 0, PaymentStatus.Paid)]
    public void ToEntityFinancialsDto_WithAgreedFeeAndPayments_ComputesBalanceAndStatus(
        decimal agreedFee,
        decimal collectedAmount,
        decimal expectedRemaining,
        PaymentStatus expectedStatus)
    {
        var transactions = collectedAmount > 0
            ? new List<FeeTransaction>
            {
                new() { Id = Guid.NewGuid(), Amount = collectedAmount, TransactionDate = new DateOnly(2026, 4, 1) }
            }
            : new List<FeeTransaction>();

        var dto = FinancialLedgerCalculator.ToEntityFinancialsDto(agreedFee, transactions);

        dto.AgreedFee.Should().Be(agreedFee);
        dto.TotalCollected.Should().Be(collectedAmount);
        dto.RemainingBalance.Should().Be(expectedRemaining);
        dto.PaymentStatus.Should().Be(expectedStatus);
    }

    [Fact]
    public void ToEntityFinancialsDto_OrdersTransactionsByDateAscending()
    {
        var transactions = new List<FeeTransaction>
        {
            new() { Id = Guid.NewGuid(), Amount = 300m, TransactionDate = new DateOnly(2026, 5, 15), ReceiptNumber = "Later" },
            new() { Id = Guid.NewGuid(), Amount = 200m, TransactionDate = new DateOnly(2026, 2, 1), ReceiptNumber = "Earlier" }
        };

        var dto = FinancialLedgerCalculator.ToEntityFinancialsDto(1000m, transactions);

        dto.Transactions.Select(t => t.ReceiptNumber).Should().ContainInOrder("Earlier", "Later");
    }
}
