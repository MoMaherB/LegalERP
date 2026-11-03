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
using LegalERP.Application.Attorneys;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Attorneys;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Attorneys;

public class AttorneyListTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeAttorneyListHttpMessageHandler _httpHandler = new();

    public AttorneyListTests()
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
        Services.AddSingleton(new AttorneyApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void AttorneyList_WhenLoaded_RendersAttorneyTableRows()
    {
        var attorneyId = Guid.NewGuid();
        _httpHandler.Attorneys =
        [
            new AttorneySummaryDto(attorneyId, "ATT-100-2026", 3)
        ];

        var cut = RenderComponent<AttorneyList>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(1);
            var rowText = rows[0].TextContent;
            rowText.Should().Contain("ATT-100-2026");
            rowText.Should().Contain("3");
        });
    }

    [Fact]
    public void AttorneyList_WhenEmpty_RendersNoAttorneysFoundAlert()
    {
        _httpHandler.Attorneys = [];

        var cut = RenderComponent<AttorneyList>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".alert-info").TextContent.Should().Contain("NoAttorneysFound");
            cut.Find(".alert-info").TextContent.Should().Contain("AddFirstAttorney");
        });
    }

    [Fact]
    public async Task AttorneyList_Search_CallsSearchApiAndUpdatesList()
    {
        _httpHandler.Attorneys =
        [
            new AttorneySummaryDto(Guid.NewGuid(), "ATT-1", 1)
        ];
        _httpHandler.SearchResults =
        [
            new AttorneySummaryDto(Guid.NewGuid(), "ATT-SEARCH-FOUND", 5)
        ];

        var cut = RenderComponent<AttorneyList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("SEARCH");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("SEARCH");
            cut.Find("tbody tr").TextContent.Should().Contain("ATT-SEARCH-FOUND");
            cut.Find("tbody tr").TextContent.Should().Contain("5");
        });
    }

    [Fact]
    public async Task AttorneyList_Search_PressingEnterInSearchInput_TriggersSearch()
    {
        _httpHandler.Attorneys =
        [
            new AttorneySummaryDto(Guid.NewGuid(), "ATT-INIT", 0)
        ];
        _httpHandler.SearchResults =
        [
            new AttorneySummaryDto(Guid.NewGuid(), "ATT-KEY-ENTER", 2)
        ];

        var cut = RenderComponent<AttorneyList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("KEY");
        await searchInput.KeyUpAsync(new KeyboardEventArgs { Key = "Enter" });

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("KEY");
            cut.Find("tbody tr").TextContent.Should().Contain("ATT-KEY-ENTER");
        });
    }

    [Fact]
    public async Task AttorneyList_Reset_ClearsSearchTermAndReloadsAllAttorneys()
    {
        _httpHandler.Attorneys =
        [
            new AttorneySummaryDto(Guid.NewGuid(), "ATT-ALL", 1)
        ];
        _httpHandler.SearchResults =
        [
            new AttorneySummaryDto(Guid.NewGuid(), "ATT-SEARCHED", 9)
        ];

        var cut = RenderComponent<AttorneyList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("SEARCHED");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("ATT-SEARCHED");
        });

        var resetBtn = cut.Find("button.ios-btn-secondary");
        await cut.InvokeAsync(() => resetBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("ATT-ALL");
        });
    }

    [Fact]
    public async Task AttorneyList_DeleteAttorney_WhenConfirmed_CallsDeleteApiShowsToastAndRefreshes()
    {
        var attorneyId = Guid.NewGuid();
        _httpHandler.Attorneys =
        [
            new AttorneySummaryDto(attorneyId, "ATT-TO-DELETE", 0)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<AttorneyList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().Be(attorneyId);
        _mockToast.Verify(t => t.ShowSuccess("AttorneyDeleted"), Times.Once);
    }

    [Fact]
    public async Task AttorneyList_DeleteAttorney_WhenCancelled_DoesNotCallDeleteApi()
    {
        var attorneyId = Guid.NewGuid();
        _httpHandler.Attorneys =
        [
            new AttorneySummaryDto(attorneyId, "ATT-KEEP", 2)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var cut = RenderComponent<AttorneyList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().BeNull();
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    private sealed class FakeAttorneyListHttpMessageHandler : HttpMessageHandler
    {
        public List<AttorneySummaryDto> Attorneys { get; set; } = [];
        public List<AttorneySummaryDto> SearchResults { get; set; } = [];
        public string? LastSearchTerm { get; private set; }
        public Guid? LastDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/api/attorneys/search?term="))
            {
                var queryIndex = path.IndexOf("term=", StringComparison.Ordinal);
                LastSearchTerm = queryIndex >= 0 ? Uri.UnescapeDataString(path.Substring(queryIndex + 5)) : null;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, SearchResults));
            }

            if (request.Method == HttpMethod.Get && (path.EndsWith("/api/attorneys") || path.EndsWith("/api/attorneys/")))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Attorneys));
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/api/attorneys/"))
            {
                var parts = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && Guid.TryParse(parts[^1], out var id))
                {
                    LastDeletedId = id;
                    Attorneys = Attorneys.Where(a => a.Id != id).ToList();
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
