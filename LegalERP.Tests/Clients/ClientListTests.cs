using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Clients;
using LegalERP.Application.Common;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Clients;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientListTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeClientListHttpMessageHandler _httpHandler = new();

    public ClientListTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("TestAdmin");
        auth.SetRoles("SuperAdmin");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void ClientList_WhenLoaded_RendersClientTableRows()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(clientId, "CL-555", "عادل إمام", "27001011234567", "01009988776")
        };

        var cut = RenderComponent<ClientList>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(1);
            var rowText = rows[0].TextContent;
            rowText.Should().Contain("CL-555");
            rowText.Should().Contain("عادل إمام");
            rowText.Should().Contain("27001011234567");
            rowText.Should().Contain("01009988776");
        });
    }

    [Fact]
    public void ClientList_WhenEmpty_RendersNoClientsFoundMessage()
    {
        _httpHandler.Clients = new List<ClientSummaryDto>();

        var cut = RenderComponent<ClientList>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".alert-info").TextContent.Should().Contain("NoClientsFound");
        });
    }

    [Fact]
    public async Task ClientList_Search_CallsSearchApiAndUpdatesList()
    {
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-1", "سامي فهمي", null, null)
        };
        _httpHandler.SearchResults = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-2", "سامح عبد الشكور", "29505051234567", null)
        };

        var cut = RenderComponent<ClientList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("سامح");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("سامح");
            cut.Find("tbody tr").TextContent.Should().Contain("سامح عبد الشكور");
        });
    }

    [Fact]
    public async Task ClientList_Reset_ClearsSearchTermAndReloadsAllClients()
    {
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-ALL", "عميل عام", null, null)
        };
        _httpHandler.SearchResults = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-SEARCH", "عميل بحث", null, null)
        };

        var cut = RenderComponent<ClientList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("بحث");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("عميل بحث");
        });

        var resetBtn = cut.Find("button.ios-btn-secondary");
        await cut.InvokeAsync(() => resetBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("عميل عام");
        });
    }

    [Fact]
    public async Task ClientList_DeleteClient_WhenConfirmed_CallsDeleteApiAndRefreshes()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(clientId, "CL-DEL", "عميل للحذف", null, null)
        };

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<ClientList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().Be(clientId);
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ClientList_WithPagination_RendersControlsAndNavigatesBetweenPages()
    {
        _httpHandler.Clients = Enumerable.Range(1, 35)
            .Select(i => new ClientSummaryDto(Guid.NewGuid(), $"CL-{i:D2}", $"عميل رقم {i}", null, null))
            .ToList();

        var cut = RenderComponent<ClientList>();

        // Page 1: 25 items rendered
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(25);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 2");

            var prevBtn = cut.Find("button.pagination-prev");
            prevBtn.HasAttribute("disabled").Should().BeTrue();

            var nextBtn = cut.Find("button.pagination-next");
            nextBtn.HasAttribute("disabled").Should().BeFalse();
        });

        // Click next -> Page 2: 10 items rendered
        var nextButton = cut.Find("button.pagination-next");
        await cut.InvokeAsync(() => nextButton.Click());

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(10);
            rows[0].TextContent.Should().Contain("CL-26");
            cut.Find(".pagination-indicator").TextContent.Should().Contain("2 / 2");

            var prevBtn = cut.Find("button.pagination-prev");
            prevBtn.HasAttribute("disabled").Should().BeFalse();

            var nextBtn = cut.Find("button.pagination-next");
            nextBtn.HasAttribute("disabled").Should().BeTrue();
        });

        // Click previous -> Page 1 again
        var prevButton = cut.Find("button.pagination-prev");
        await cut.InvokeAsync(() => prevButton.Click());

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(25);
            rows[0].TextContent.Should().Contain("CL-01");
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 2");
        });
    }

    [Fact]
    public async Task ClientList_WhenPageSizeChanged_UpdatesPageAndItemsCount()
    {
        _httpHandler.Clients = Enumerable.Range(1, 25)
            .Select(i => new ClientSummaryDto(Guid.NewGuid(), $"CL-{i:D2}", $"عميل {i}", null, null))
            .ToList();

        var cut = RenderComponent<ClientList>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(25);
        });

        // Change page size to 10
        var select = cut.Find("select.ios-select.form-select-sm");
        await cut.InvokeAsync(() => select.Change("10"));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(10);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 3");
        });
    }

    [Fact]
    public async Task ClientList_PreservesFiltersAcrossPagination()
    {
        _httpHandler.SearchResults = Enumerable.Range(1, 30)
            .Select(i => new ClientSummaryDto(Guid.NewGuid(), $"CL-SRCH-{i:D2}", $"عميل بحث {i}", null, null))
            .ToList();

        var cut = RenderComponent<ClientList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("بحث");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(25);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 2");
        });

        // Click next on filtered results
        var nextBtn = cut.Find("button.pagination-next");
        await cut.InvokeAsync(() => nextBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("بحث");
            cut.FindAll("tbody tr").Should().HaveCount(5);
            cut.Find("tbody tr").TextContent.Should().Contain("CL-SRCH-26");
            cut.Find(".pagination-indicator").TextContent.Should().Contain("2 / 2");
        });
    }

    [Fact]
    public async Task ClientList_WhenSearchInputChangedWithoutSubmitting_PaginationPreservesAppliedSearchTerm()
    {
        _httpHandler.SearchResults = Enumerable.Range(1, 30)
            .Select(i => new ClientSummaryDto(Guid.NewGuid(), $"CL-SRCH-{i:D2}", $"عميل بحث {i}", null, null))
            .ToList();

        var cut = RenderComponent<ClientList>();

        // 1. Submit search for "بحث"
        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("بحث");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(25);
        });

        // 2. Type "unsubmitted" into search input without clicking Search
        searchInput.Input("unsubmitted");

        // 3. Click Next page -> Should still page against committed query "بحث"
        var nextBtn = cut.Find("button.pagination-next");
        await cut.InvokeAsync(() => nextBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("بحث");
            cut.FindAll("tbody tr").Should().HaveCount(5);
        });
    }

    [Fact]
    public void ClientList_WhenOutOfRangePage_PreviousAndNextButtonsRemainDisabled()
    {
        _httpHandler.Clients = Enumerable.Range(1, 5)
            .Select(i => new ClientSummaryDto(Guid.NewGuid(), $"CL-{i}", $"عميل {i}", null, null))
            .ToList();

        var cut = RenderComponent<ClientList>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(5);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 1");
            cut.Find("button.pagination-prev").HasAttribute("disabled").Should().BeTrue();
            cut.Find("button.pagination-next").HasAttribute("disabled").Should().BeTrue();
        });
    }

    private sealed class FakeClientListHttpMessageHandler : HttpMessageHandler
    {
        public List<ClientSummaryDto> Clients { get; set; } = new();
        public List<ClientSummaryDto> SearchResults { get; set; } = new();
        public string? LastSearchTerm { get; private set; }
        public Guid? LastDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/api/clients/search"))
            {
                var uri = request.RequestUri!;
                var queryParams = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.OrdinalIgnoreCase);

                queryParams.TryGetValue("term", out var term);
                LastSearchTerm = term;

                int pageNumber = queryParams.TryGetValue("pageNumber", out var pageStr) && int.TryParse(pageStr, out var parsedPage) ? parsedPage : 1;
                int pageSize = queryParams.TryGetValue("pageSize", out var sizeStr) && int.TryParse(sizeStr, out var parsedSize) ? parsedSize : 25;

                var source = string.IsNullOrWhiteSpace(term) ? Clients : SearchResults;

                if (path.Contains("/paged") || queryParams.ContainsKey("pageNumber"))
                {
                    var pagedItems = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, new PagedResult<ClientSummaryDto>(pagedItems, source.Count, pageNumber, pageSize)));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, source));
            }

            if (request.Method == HttpMethod.Get && (path.Contains("/api/clients/paged") || path.Contains("/api/clients")))
            {
                var uri = request.RequestUri!;
                var queryParams = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.OrdinalIgnoreCase);

                int pageNumber = queryParams.TryGetValue("pageNumber", out var pageStr) && int.TryParse(pageStr, out var parsedPage) ? parsedPage : 1;
                int pageSize = queryParams.TryGetValue("pageSize", out var sizeStr) && int.TryParse(sizeStr, out var parsedSize) ? parsedSize : 25;

                if (path.Contains("/paged") || queryParams.ContainsKey("pageNumber"))
                {
                    var pagedItems = Clients.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, new PagedResult<ClientSummaryDto>(pagedItems, Clients.Count, pageNumber, pageSize)));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Clients));
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/api/clients/"))
            {
                var parts = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && Guid.TryParse(parts[^1], out var id))
                {
                    LastDeletedId = id;
                    Clients = Clients.Where(c => c.Id != id).ToList();
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
            };
        }
    }
}
