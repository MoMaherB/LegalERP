using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompaniesControllerPaginationTests
{
    private readonly Mock<ICompanyRepository> _mockRepo = new();
    private readonly CompaniesController _controller;
    private readonly DefaultHttpContext _httpContext = new();

    public CompaniesControllerPaginationTests()
    {
        _controller = new CompaniesController(_mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    [Fact]
    public async Task GetAll_WhenNoPagination_ReturnsListAndDoesNotSetPaginationHeaders()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Company A", Category = CompanyCategory.SoleProprietorship },
            new() { Id = Guid.NewGuid(), CompanyName = "Company B", Category = CompanyCategory.Partnership }
        };
        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(companies);

        var result = await _controller.GetAll(null, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<CompanyDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].CompanyName.Should().Be("Company A");

        _httpContext.Response.Headers.ContainsKey("X-Total-Count").Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_WhenPageNumberProvided_ReturnsPagedResultAndSetsHeaders()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Company A", Category = CompanyCategory.CapitalCompany }
        };
        var paged = new PagedResult<Company>(companies, 45, 1, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(1, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.TotalCount.Should().Be(45);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(25);
        pagedResult.TotalPages.Should().Be(2);
        pagedResult.Items.Should().HaveCount(1);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("45");
        _httpContext.Response.Headers["X-Page-Number"].ToString().Should().Be("1");
        _httpContext.Response.Headers["X-Page-Size"].ToString().Should().Be("25");
        _httpContext.Response.Headers["X-Total-Pages"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task GetAll_WhenOnlyPageSizeProvided_DefaultsPageNumberToOne()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Company A" }
        };
        var paged = new PagedResult<Company>(companies, 10, 1, 10);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(null, 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetPaged_ReturnsTypedPagedResultAndSetsHeaders()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Company 10" }
        };
        var paged = new PagedResult<Company>(companies, 60, 2, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 2 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetPaged(2, 25, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.TotalCount.Should().Be(60);
        pagedResult.PageNumber.Should().Be(2);
        pagedResult.TotalPages.Should().Be(3);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("60");
        _httpContext.Response.Headers["X-Page-Number"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task Search_WhenNoPagination_ReturnsList()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Company Search" }
        };
        _mockRepo.Setup(r => r.SearchAsync("test", CompanyCategory.CapitalCompany, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companies);

        var result = await _controller.Search("test", CompanyCategory.CapitalCompany, null, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<CompanyDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);
        dtos[0].CompanyName.Should().Be("Company Search");
    }

    [Fact]
    public async Task Search_WhenPaginationProvided_ReturnsPagedResultAndSetsHeaders()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Company Paged Search" }
        };
        var paged = new PagedResult<Company>(companies, 30, 2, 10);
        _mockRepo.Setup(r => r.SearchPagedAsync("test", CompanyCategory.SoleProprietorship, It.Is<PaginationParams>(p => p.PageNumber == 2 && p.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.Search("test", CompanyCategory.SoleProprietorship, 2, 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.TotalCount.Should().Be(30);
        pagedResult.PageNumber.Should().Be(2);
        pagedResult.PageSize.Should().Be(10);
        pagedResult.TotalPages.Should().Be(3);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("30");
        _httpContext.Response.Headers["X-Total-Pages"].ToString().Should().Be("3");
    }

    [Fact]
    public async Task SearchPaged_ReturnsTypedPagedResultAndSetsHeaders()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Typed Company Search" }
        };
        var paged = new PagedResult<Company>(companies, 80, 1, 20);
        _mockRepo.Setup(r => r.SearchPagedAsync("typed", null, It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 20), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.SearchPaged("typed", null, 1, 20, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.TotalCount.Should().Be(80);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.TotalPages.Should().Be(4);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("80");
    }

    [Fact]
    public async Task GetAll_WhenSearchTermAndCategoryPassed_DelegatesToSearchPaged()
    {
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), CompanyName = "Delegated Search" }
        };
        var paged = new PagedResult<Company>(companies, 1, 1, 25);
        _mockRepo.Setup(r => r.SearchPagedAsync("delegated", CompanyCategory.CapitalCompany, It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(1, 25, CancellationToken.None, "delegated", CompanyCategory.CapitalCompany);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.Items.Should().HaveCount(1);
        pagedResult.Items[0].CompanyName.Should().Be("Delegated Search");
    }

    [Fact]
    public async Task GetAll_WhenNegativePageNumberProvided_ClampsPageNumberToOne()
    {
        var paged = new PagedResult<Company>([], 0, 1, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(-5, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetPaged_WhenPageNumberOutOfBounds_ReturnsEmptyItemsAndValidTotals()
    {
        var paged = new PagedResult<Company>([], 20, 10, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 10 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetPaged(10, 25, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CompanyDto>>().Subject;
        pagedResult.Items.Should().BeEmpty();
        pagedResult.TotalCount.Should().Be(20);
        pagedResult.TotalPages.Should().Be(1);
        pagedResult.PageNumber.Should().Be(10);
    }
}
