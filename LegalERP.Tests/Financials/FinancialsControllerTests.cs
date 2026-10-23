using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Application.Financials;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Financials;

public class FinancialsControllerTests
{
    private readonly Mock<ICaseRepository> _mockCaseRepo = new();
    private readonly Mock<ICompanyRepository> _mockCompanyRepo = new();
    private readonly FinancialsController _controller;

    public FinancialsControllerTests()
    {
        _controller = new FinancialsController(_mockCaseRepo.Object, _mockCompanyRepo.Object);
    }

    [Fact]
    public async Task GetSummary_AggregatesTotalsAndResolvesCompanyRegistrationAndFileNumber()
    {
        var cases = new List<Case>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CaseNumber = "CASE-101",
                Title = "Civil Lawsuit",
                AgreedFee = 5000m,
                FeeTransactions = new List<FeeTransaction>
                {
                    new() { Id = Guid.NewGuid(), Amount = 2000m, TransactionDate = new DateOnly(2026, 1, 5) }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                CaseNumber = "CASE-102",
                Title = "Unagreed Fee Case",
                AgreedFee = null,
                FeeTransactions = new List<FeeTransaction>
                {
                    new() { Id = Guid.NewGuid(), Amount = 300m, TransactionDate = new DateOnly(2026, 2, 1) }
                }
            }
        };

        var companies = new List<Company>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CompanyName = "Al-Nour Trading",
                RegistrationNumber = "CR-778899",
                FileNumber = "FILE-10",
                AgreedFee = 8000m,
                FeeTransactions = new List<FeeTransaction>
                {
                    new() { Id = Guid.NewGuid(), Amount = 8000m, TransactionDate = new DateOnly(2026, 3, 1) }
                }
            },
            new()
            {
                Id = Guid.NewGuid(),
                CompanyName = "Fallback File Company",
                RegistrationNumber = null,
                FileNumber = "FILE-99",
                AgreedFee = 1500m,
                FeeTransactions = new List<FeeTransaction>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                CompanyName = "Unregistered Company",
                RegistrationNumber = null,
                FileNumber = null,
                CommercialRegisterNumber = "LEGACY-IGNORED",
                AgreedFee = 500m,
                FeeTransactions = new List<FeeTransaction>()
            }
        };

        _mockCaseRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cases);
        _mockCompanyRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(companies);

        var actionResult = await _controller.GetSummary(CancellationToken.None);

        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var summary = okResult.Value.Should().BeOfType<GlobalFinancialsDto>().Subject;

        summary.TotalCaseAgreedFees.Should().Be(5000m);
        summary.TotalCaseCollected.Should().Be(2300m);
        summary.TotalCompanyAgreedFees.Should().Be(10000m);
        summary.TotalCompanyCollected.Should().Be(8000m);
        summary.FirmTotalAgreedFees.Should().Be(15000m);
        summary.FirmTotalCollected.Should().Be(10300m);

        var alNour = summary.Records.Single(r => r.OwnerName == "Al-Nour Trading");
        alNour.ReferenceNumber.Should().Be("CR-778899");
        alNour.PaymentStatus.Should().Be(PaymentStatus.Paid);
        alNour.RemainingBalance.Should().Be(0m);

        var fallbackCompany = summary.Records.Single(r => r.OwnerName == "Fallback File Company");
        fallbackCompany.ReferenceNumber.Should().Be("FILE-99");
        fallbackCompany.PaymentStatus.Should().Be(PaymentStatus.Unpaid);
        fallbackCompany.RemainingBalance.Should().Be(1500m);

        var unregisteredCompany = summary.Records.Single(r => r.OwnerName == "Unregistered Company");
        unregisteredCompany.ReferenceNumber.Should().Be("N/A");

        var unagreedCase = summary.Records.Single(r => r.OwnerName == "Unagreed Fee Case");
        unagreedCase.RemainingBalance.Should().Be(0m);
        unagreedCase.PaymentStatus.Should().Be(PaymentStatus.Unpaid);
    }
}
