using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Clients;
using LegalERP.Application.Common;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientsControllerPaginationTests
{
    private readonly Mock<IClientRepository> _mockRepo = new();
    private readonly ClientsController _controller;
    private readonly DefaultHttpContext _httpContext = new();

    public ClientsControllerPaginationTests()
    {
        _controller = new ClientsController(_mockRepo.Object)
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
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FileNumber = "CL-1", FullName = "Client 1" },
            new() { Id = Guid.NewGuid(), FileNumber = "CL-2", FullName = "Client 2" }
        };
        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clients);

        var result = await _controller.GetAll(null, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ClientSummaryDto>>().Subject.ToList();
        dtos.Should().HaveCount(2);
        dtos[0].FullName.Should().Be("Client 1");

        _httpContext.Response.Headers.ContainsKey("X-Total-Count").Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_WhenPageNumberProvided_ReturnsPagedResultAndSetsHeaders()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FileNumber = "CL-1", FullName = "Client 1" }
        };
        var paged = new PagedResult<Client>(clients, 55, 1, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(1, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.TotalCount.Should().Be(55);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(25);
        pagedResult.TotalPages.Should().Be(3);
        pagedResult.Items.Should().HaveCount(1);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("55");
        _httpContext.Response.Headers["X-Page-Number"].ToString().Should().Be("1");
        _httpContext.Response.Headers["X-Page-Size"].ToString().Should().Be("25");
        _httpContext.Response.Headers["X-Total-Pages"].ToString().Should().Be("3");
    }

    [Fact]
    public async Task GetAll_WhenOnlyPageSizeProvided_DefaultsPageNumberToOne()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FullName = "Client A" }
        };
        var paged = new PagedResult<Client>(clients, 10, 1, 10);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(null, 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAll_WhenSearchTermProvided_DelegatesToSearchPaged()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FullName = "Searched Client" }
        };
        var paged = new PagedResult<Client>(clients, 1, 1, 25);
        _mockRepo.Setup(r => r.SearchPagedAsync("search", It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(1, 25, CancellationToken.None, "search");

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.Items.Should().HaveCount(1);
        pagedResult.Items[0].FullName.Should().Be("Searched Client");
    }

    [Fact]
    public async Task GetPaged_ReturnsTypedPagedResultAndSetsHeaders()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FullName = "Client 10" }
        };
        var paged = new PagedResult<Client>(clients, 70, 2, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 2 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetPaged(2, 25, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.TotalCount.Should().Be(70);
        pagedResult.PageNumber.Should().Be(2);
        pagedResult.TotalPages.Should().Be(3);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("70");
        _httpContext.Response.Headers["X-Page-Number"].ToString().Should().Be("2");
    }

    [Fact]
    public async Task Search_WhenNoPagination_ReturnsList()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FullName = "Search Only Client" }
        };
        _mockRepo.Setup(r => r.SearchAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(clients);

        var result = await _controller.Search("test", null, null, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<ClientSummaryDto>>().Subject.ToList();
        dtos.Should().HaveCount(1);
        dtos[0].FullName.Should().Be("Search Only Client");
    }

    [Fact]
    public async Task Search_WhenPaginationProvided_ReturnsPagedResultAndSetsHeaders()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FullName = "Client Paged Search" }
        };
        var paged = new PagedResult<Client>(clients, 35, 2, 10);
        _mockRepo.Setup(r => r.SearchPagedAsync("test", It.Is<PaginationParams>(p => p.PageNumber == 2 && p.PageSize == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.Search("test", 2, 10, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.TotalCount.Should().Be(35);
        pagedResult.PageNumber.Should().Be(2);
        pagedResult.PageSize.Should().Be(10);
        pagedResult.TotalPages.Should().Be(4);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("35");
        _httpContext.Response.Headers["X-Total-Pages"].ToString().Should().Be("4");
    }

    [Fact]
    public async Task SearchPaged_ReturnsTypedPagedResultAndSetsHeaders()
    {
        var clients = new List<Client>
        {
            new() { Id = Guid.NewGuid(), FullName = "Typed Client Search" }
        };
        var paged = new PagedResult<Client>(clients, 80, 1, 20);
        _mockRepo.Setup(r => r.SearchPagedAsync("typed", It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 20), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.SearchPaged("typed", 1, 20, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.TotalCount.Should().Be(80);
        pagedResult.PageNumber.Should().Be(1);
        pagedResult.TotalPages.Should().Be(4);

        _httpContext.Response.Headers["X-Total-Count"].ToString().Should().Be("80");
    }

    [Fact]
    public async Task GetAll_WhenNegativePageNumberProvided_ClampsPageNumberToOne()
    {
        var paged = new PagedResult<Client>([], 0, 1, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 1 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetAll(-3, 25, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetPaged_WhenPageNumberOutOfBounds_ReturnsEmptyItemsAndValidTotals()
    {
        var paged = new PagedResult<Client>([], 15, 5, 25);
        _mockRepo.Setup(r => r.GetPagedAsync(It.Is<PaginationParams>(p => p.PageNumber == 5 && p.PageSize == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var result = await _controller.GetPaged(5, 25, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<ClientSummaryDto>>().Subject;
        pagedResult.Items.Should().BeEmpty();
        pagedResult.TotalCount.Should().Be(15);
        pagedResult.TotalPages.Should().Be(1);
        pagedResult.PageNumber.Should().Be(5);
    }
}
