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

    [Fact]
    public async Task GetEntityFinancials_ForCompanyOwner_ReturnsLedgerFromCompany()
    {
        var companyId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            CompanyName = "Ledger Co",
            AgreedFee = 2000m,
            FeeTransactions = new List<FeeTransaction>
            {
                new() { Id = Guid.NewGuid(), Amount = 500m, TransactionDate = new DateOnly(2026, 6, 1), CompanyId = companyId }
            }
        };
        _mockCompanyRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var result = await _controller.GetEntityFinancials("company", companyId, CancellationToken.None);

        var dto = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<EntityFinancialsDto>().Subject;
        dto.AgreedFee.Should().Be(2000m);
        dto.TotalCollected.Should().Be(500m);
        dto.RemainingBalance.Should().Be(1500m);
        dto.PaymentStatus.Should().Be(PaymentStatus.PartiallyPaid);
    }

    [Fact]
    public async Task GetEntityFinancials_WhenCaseMissing_ReturnsNotFound()
    {
        var caseId = Guid.NewGuid();
        _mockCaseRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync((Case?)null);

        var result = await _controller.GetEntityFinancials("case", caseId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEntityFinancials_WithUnknownOwnerType_ReturnsBadRequest()
    {
        var result = await _controller.GetEntityFinancials("client", Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateAgreedFee_ForCase_SetsFeeAndSaves()
    {
        var caseId = Guid.NewGuid();
        var caseEntity = new Case { Id = caseId, CaseNumber = "C-1", Title = "Fee Case", AgreedFee = null };
        _mockCaseRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(caseEntity);

        var result = await _controller.UpdateAgreedFee("case", caseId, new UpdateAgreedFeeDto(7500m), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        caseEntity.AgreedFee.Should().Be(7500m);
        _mockCaseRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAgreedFee_ForCompany_SetsFeeAndSaves()
    {
        var companyId = Guid.NewGuid();
        var company = new Company { Id = companyId, CompanyName = "Fee Company", AgreedFee = null };
        _mockCompanyRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var result = await _controller.UpdateAgreedFee("company", companyId, new UpdateAgreedFeeDto(9200m), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        company.AgreedFee.Should().Be(9200m);
        _mockCompanyRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddFeeTransaction_ForCase_LinksTransactionToCaseAndReturnsDto()
    {
        var caseId = Guid.NewGuid();
        _mockCaseRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Case { Id = caseId, CaseNumber = "C-88", Title = "Litigation Case" });
        FeeTransaction? added = null;
        _mockCaseRepo.Setup(r => r.AddFeeTransactionAsync(It.IsAny<FeeTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<FeeTransaction, CancellationToken>((t, _) => added = t)
            .Returns(Task.CompletedTask);

        var dto = new AddFeeTransactionDto { Amount = 3500m, TransactionDate = new DateOnly(2026, 7, 10), ReceiptNumber = "RC-CASE-1" };
        var result = await _controller.AddFeeTransaction("case", caseId, dto, CancellationToken.None);

        var returned = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<FeeTransactionDto>().Subject;
        returned.Amount.Should().Be(3500m);
        returned.ReceiptNumber.Should().Be("RC-CASE-1");
        added.Should().NotBeNull();
        added!.CaseId.Should().Be(caseId);
        added.CompanyId.Should().BeNull();
        _mockCaseRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddFeeTransaction_ForCompany_LinksTransactionToCompanyAndReturnsDto()
    {
        var companyId = Guid.NewGuid();
        _mockCompanyRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Company { Id = companyId, CompanyName = "Payer Co" });
        FeeTransaction? added = null;
        _mockCompanyRepo.Setup(r => r.AddFeeTransactionAsync(It.IsAny<FeeTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<FeeTransaction, CancellationToken>((t, _) => added = t)
            .Returns(Task.CompletedTask);

        var dto = new AddFeeTransactionDto { Amount = 1200m, TransactionDate = new DateOnly(2026, 7, 1), ReceiptNumber = "RC-55" };
        var result = await _controller.AddFeeTransaction("company", companyId, dto, CancellationToken.None);

        var returned = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<FeeTransactionDto>().Subject;
        returned.Amount.Should().Be(1200m);
        returned.ReceiptNumber.Should().Be("RC-55");
        added.Should().NotBeNull();
        added!.CompanyId.Should().Be(companyId);
        added.CaseId.Should().BeNull();
        _mockCompanyRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFeeTransaction_WhenTransactionBelongsToAnotherCase_ReturnsNotFound()
    {
        var caseId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _mockCaseRepo.Setup(r => r.GetFeeTransactionByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeeTransaction { Id = transactionId, CaseId = Guid.NewGuid(), Amount = 100m });

        var result = await _controller.DeleteFeeTransaction("case", caseId, transactionId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _mockCaseRepo.Verify(r => r.SoftDeleteFeeTransaction(It.IsAny<FeeTransaction>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFeeTransaction_ForOwningCase_SoftDeletesAndSaves()
    {
        var caseId = Guid.NewGuid();
        var transaction = new FeeTransaction { Id = Guid.NewGuid(), CaseId = caseId, Amount = 100m };
        _mockCaseRepo.Setup(r => r.GetFeeTransactionByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _controller.DeleteFeeTransaction("case", caseId, transaction.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockCaseRepo.Verify(r => r.SoftDeleteFeeTransaction(transaction), Times.Once);
        _mockCaseRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFeeTransaction_ForOwningCompany_SoftDeletesAndSaves()
    {
        var companyId = Guid.NewGuid();
        var transaction = new FeeTransaction { Id = Guid.NewGuid(), CompanyId = companyId, Amount = 450m };
        _mockCompanyRepo.Setup(r => r.GetFeeTransactionByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var result = await _controller.DeleteFeeTransaction("company", companyId, transaction.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockCompanyRepo.Verify(r => r.SoftDeleteFeeTransaction(transaction), Times.Once);
        _mockCompanyRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
