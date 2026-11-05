using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Application.Financials;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Moq;
using Xunit;

namespace LegalERP.Tests.Financials;

public class FinancialsControllerCachingTests
{
    private readonly Mock<ICaseRepository> _mockCaseRepo = new();
    private readonly Mock<ICompanyRepository> _mockCompanyRepo = new();
    private readonly Mock<IFinancialSummaryCache> _mockCache = new();
    private readonly FinancialsController _controller;
    private readonly DefaultHttpContext _httpContext = new();

    public FinancialsControllerCachingTests()
    {
        _mockCache.Setup(c => c.SlidingExpirationSeconds).Returns(60);
        _controller = new FinancialsController(_mockCaseRepo.Object, _mockCompanyRepo.Object, _mockCache.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    [Fact]
    public async Task GetSummary_EmitsCacheControlAndETagHeaders()
    {
        var summary = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 5000m,
            TotalCaseCollected = 2000m,
            TotalCompanyAgreedFees = 10000m,
            TotalCompanyCollected = 5000m,
            Records = new List<FinancialRecordDto>
            {
                new() { OwnerId = Guid.NewGuid(), AgreedFee = 5000m, TotalCollected = 2000m }
            }
        };

        _mockCache.Setup(c => c.GetOrCreateSummaryAsync(It.IsAny<Func<CancellationToken, Task<GlobalFinancialsDto>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var result = await _controller.GetSummary(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(summary);

        _httpContext.Response.Headers.ContainsKey(HeaderNames.ETag).Should().BeTrue();
        _httpContext.Response.Headers.ContainsKey(HeaderNames.CacheControl).Should().BeTrue();
        _httpContext.Response.Headers[HeaderNames.CacheControl].ToString().Should().Contain("max-age=60");
    }

    [Fact]
    public async Task GetSummary_WhenIfNoneMatchMatchesETag_Returns304NotModifiedWithCachingHeaders()
    {
        var summary = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 1000m,
            TotalCaseCollected = 500m
        };

        _mockCache.Setup(c => c.GetOrCreateSummaryAsync(It.IsAny<Func<CancellationToken, Task<GlobalFinancialsDto>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        var expectedEtag = summary.ComputeETag();
        _httpContext.Request.Headers[HeaderNames.IfNoneMatch] = expectedEtag;

        var result = await _controller.GetSummary(CancellationToken.None);

        var statusResult = result.Result.Should().BeOfType<StatusCodeResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status304NotModified);

        _httpContext.Response.Headers[HeaderNames.ETag].ToString().Should().Be(expectedEtag);
        _httpContext.Response.Headers[HeaderNames.CacheControl].ToString().Should().Contain("max-age=60");
    }

    [Fact]
    public async Task CasesController_Delete_InvalidatesFinancialSummaryCache()
    {
        var caseId = Guid.NewGuid();
        var caseEntity = new Case { Id = caseId };
        _mockCaseRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseEntity);

        var casesController = new CasesController(_mockCaseRepo.Object, _mockCache.Object);
        var result = await casesController.Delete(caseId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockCache.Verify(c => c.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task CompaniesController_Delete_InvalidatesFinancialSummaryCache()
    {
        var companyId = Guid.NewGuid();
        var company = new Company { Id = companyId };
        _mockCompanyRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var companiesController = new CompaniesController(_mockCompanyRepo.Object, _mockCache.Object);
        var result = await companiesController.Delete(companyId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockCache.Verify(c => c.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task UpdateAgreedFee_WhenSuccessful_CallsInvalidateOnCache()
    {
        var caseId = Guid.NewGuid();
        var caseEntity = new Case { Id = caseId, AgreedFee = 1000m };
        _mockCaseRepo.Setup(r => r.GetByIdAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caseEntity);

        var response = await _controller.UpdateAgreedFee("case", caseId, new UpdateAgreedFeeDto(2000m), CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
        _mockCache.Verify(c => c.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task AddFeeTransaction_WhenSuccessful_CallsInvalidateOnCache()
    {
        var companyId = Guid.NewGuid();
        var company = new Company { Id = companyId };
        _mockCompanyRepo.Setup(r => r.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var dto = new AddFeeTransactionDto
        {
            Amount = 1500m,
            TransactionDate = new DateOnly(2026, 4, 1),
            ReceiptNumber = "REC-1",
            Notes = "Notes"
        };
        var response = await _controller.AddFeeTransaction("company", companyId, dto, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        _mockCache.Verify(c => c.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task DeleteFeeTransaction_WhenSuccessful_CallsInvalidateOnCache()
    {
        var caseId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var transaction = new FeeTransaction { Id = transactionId, CaseId = caseId, Amount = 500m };

        _mockCaseRepo.Setup(r => r.GetFeeTransactionByIdAsync(transactionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        var response = await _controller.DeleteFeeTransaction("case", caseId, transactionId, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
        _mockCache.Verify(c => c.Invalidate(), Times.Once);
    }
}
