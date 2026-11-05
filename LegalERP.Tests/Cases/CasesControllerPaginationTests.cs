using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Cases;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CasesControllerPaginationTests
{
    private readonly Mock<ICaseRepository> _mockRepo = new();
    private readonly CasesController _controller;
    private readonly DefaultHttpContext _httpContext = new();

    public CasesControllerPaginationTests()
    {
        _controller = new CasesController(_mockRepo.Object)
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
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-1", Title = "Title 1" },
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-2", Title = "Title 2" }
        };
        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cases);

        var result = await _controller.GetAll(null, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<CaseDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].CaseNumber.Should().Be("CASE-1");

        _httpContext.Response.Headers.ContainsKey("X-Total-Count").Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_WhenPageNumberProvided_ReturnsPagedResultAndSetsHeaders()
    {
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-1", Title = "Title 1" },
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-2", Title = "Title 2" }
        };
        var paged = new PagedResult<Case>(cases, 50, 1, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(1, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CaseDto>>().Subject;
        pagedResult.TotalCount.Should().Be(50);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(25);
        pagedResult.TotalPages.Should().Be(2);
        pagedResult.Items.Should().HaveCount(2);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("50");
        _httpContext.Response.Headers["X-Page-Number"].ToString().Should().Be("1");
        _httpContext.Response.Headers["X-Page-Size"].ToString().Should().Be("25");
        _httpContext.Response.Headers["X-Total-Pages"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task GetAll_WhenOnlyPageSizeProvided_DefaultsPageNumberToOne()
    {
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-1", Title = "Title 1" }
        };
        var paged = new PagedResult<Case>(cases, 10, 1, 10);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(null, 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CaseDto>>().Subject;
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAll_WhenEmptyResults_ReturnsEmptyPagedResultWithZeroTotals()
    {
        var paged = new PagedResult<Case>([], 0, 1, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(1, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CaseDto>>().Subject;
        pagedResult.TotalCount.Should().Be(0);
        pagedResult.TotalPages.Should().Be(0);
        pagedResult.Items.Should().BeEmpty();
        pagedResult.HasPreviousPage.Should().BeFalse();
        pagedResult.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetPaged_ReturnsTypedPagedResultAndSetsHeaders()
    {
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-10", Title = "Title 10" }
        };
        var paged = new PagedResult<Case>(cases, 75, 2, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 2 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetPaged(2, 25, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CaseDto>>().Subject;
        pagedResult.TotalCount.Should().Be(75);
        pagedResult.PageNumber.Should().Be(2);
        pagedResult.TotalPages.Should().Be(3);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("75");
        _httpContext.Response.Headers["X-Page-Number"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task Search_WhenNoPagination_ReturnsList()
    {
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-SRCH", Title = "Searched" }
        };
        _mockRepo.Setup(r => r.SearchAsync("test", CaseType.Civil, CaseStatus.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cases);

        var result = await _controller.Search("test", CaseType.Civil, CaseStatus.Active, null, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<CaseDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);
        dtos[0].CaseNumber.Should().Be("CASE-SRCH");
    }

    [Fact]
    public async Task Search_WhenPaginationProvided_ReturnsPagedResultAndSetsHeaders()
    {
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-SRCH-PAGED", Title = "Searched Paged" }
        };
        var paged = new PagedResult<Case>(cases, 30, 2, 10);
        _mockRepo.Setup(r => r.SearchPagedAsync("test", CaseType.Civil, CaseStatus.Active, It.Is<PaginationParams>(p => p.PageNumber == 2 && p.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.Search("test", CaseType.Civil, CaseStatus.Active, 2, 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CaseDto>>().Subject;
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
        var cases = new List<Case>
        {
            new() { Id = Guid.NewGuid(), CaseNumber = "CASE-PAGED-TYPED", Title = "Typed Search" }
        };
        var paged = new PagedResult<Case>(cases, 40, 1, 20);
        _mockRepo.Setup(r => r.SearchPagedAsync("typed", null, null, It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 20), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.SearchPaged("typed", null, null, 1, 20, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CaseDto>>().Subject;
        pagedResult.TotalCount.Should().Be(40);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.TotalPages.Should().Be(2);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("40");
    }
}
