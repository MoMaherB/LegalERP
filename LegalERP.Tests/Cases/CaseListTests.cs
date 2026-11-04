using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Cases;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Cases;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CaseListTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeCaseListHttpMessageHandler _httpHandler = new();

    public CaseListTests()
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
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void CaseList_WhenLoaded_RendersCaseTableRowsWithStatusAndOutcomeBadges()
    {
        _httpHandler.Cases =
        [
            CreateCaseDto(Guid.NewGuid(), "CASE-ACT", "قضية نشطة", CaseType.Civil, CaseStatus.Active, courtName: "محكمة الجيزة"),
            CreateCaseDto(Guid.NewGuid(), "CASE-WON", "قضية كسبانة", CaseType.Criminal, CaseStatus.Closed, CaseOutcome.Won),
            CreateCaseDto(Guid.NewGuid(), "CASE-LOST", "قضية خسرانة", CaseType.PersonalStatus, CaseStatus.Closed, CaseOutcome.Lost),
            CreateCaseDto(Guid.NewGuid(), "CASE-SET", "قضية صلح", CaseType.Administrative, CaseStatus.Closed, CaseOutcome.Settled)
        ];

        var cut = RenderComponent<CaseList>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(4);

            // Row 0: Active -> amber
            rows[0].TextContent.Should().Contain("CASE-ACT");
            rows[0].QuerySelector(".ios-status-amber")?.TextContent.Should().Contain("Active");

            // Row 1: ClosedWon -> green
            rows[1].TextContent.Should().Contain("CASE-WON");
            rows[1].QuerySelector(".ios-status-green")?.TextContent.Should().Contain("ClosedWon");

            // Row 2: ClosedLost -> red
            rows[2].TextContent.Should().Contain("CASE-LOST");
            rows[2].QuerySelector(".ios-status-red")?.TextContent.Should().Contain("ClosedLost");

            // Row 3: ClosedSettled -> blue
            rows[3].TextContent.Should().Contain("CASE-SET");
            rows[3].QuerySelector(".ios-status-blue")?.TextContent.Should().Contain("ClosedSettled");
        });
    }

    [Fact]
    public void CaseList_WhenEmpty_RendersNoCasesFoundAlert()
    {
        _httpHandler.Cases = [];

        var cut = RenderComponent<CaseList>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".alert-info").TextContent.Should().Contain("NoCasesFound");
        });
    }

    [Fact]
    public async Task CaseList_Search_SendsTermAndUpdatesTable()
    {
        _httpHandler.Cases =
        [
            CreateCaseDto(Guid.NewGuid(), "CASE-1", "قضية عادية")
        ];
        _httpHandler.SearchResults =
        [
            CreateCaseDto(Guid.NewGuid(), "CASE-SEARCHED", "قضية البحث المطلوبة")
        ];

        var cut = RenderComponent<CaseList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("المطلوبة");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("المطلوبة");
            cut.Find("tbody tr").TextContent.Should().Contain("CASE-SEARCHED");
        });
    }

    [Fact]
    public async Task CaseList_Reset_ClearsFiltersAndReloadsAllCases()
    {
        _httpHandler.Cases =
        [
            CreateCaseDto(Guid.NewGuid(), "CASE-ALL", "قضية عامة")
        ];
        _httpHandler.SearchResults =
        [
            CreateCaseDto(Guid.NewGuid(), "CASE-FILTERED", "قضية مصفاة")
        ];

        var cut = RenderComponent<CaseList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("مصفاة");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("CASE-FILTERED");
        });

        var resetBtn = cut.Find("button.ios-btn-secondary");
        await cut.InvokeAsync(() => resetBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("CASE-ALL");
        });
    }

    [Fact]
    public async Task CaseList_DeleteCase_WhenConfirmed_CallsDeleteApiShowsToastAndRefreshes()
    {
        var caseId = Guid.NewGuid();
        _httpHandler.Cases =
        [
            CreateCaseDto(caseId, "CASE-DEL", "قضية للحذف")
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<CaseList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().Be(caseId);
        _mockToast.Verify(t => t.ShowSuccess("CaseDeleted"), Times.Once);
    }

    [Fact]
    public async Task CaseList_DeleteCase_WhenCancelled_DoesNotCallDeleteApi()
    {
        var caseId = Guid.NewGuid();
        _httpHandler.Cases =
        [
            CreateCaseDto(caseId, "CASE-KEEP", "قضية باقية")
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var cut = RenderComponent<CaseList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().BeNull();
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    private static CaseDto CreateCaseDto(
        Guid id,
        string caseNumber,
        string title,
        CaseType caseType = CaseType.Civil,
        CaseStatus status = CaseStatus.Active,
        CaseOutcome? outcome = null,
        string? courtName = null)
    {
        return new CaseDto(
            id,
            caseNumber,
            title,
            caseType,
            status,
            outcome,
            null,
            courtName,
            null,
            null,
            [],
            [],
            []
        );
    }

    private sealed class FakeCaseListHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public List<CaseDto> Cases { get; set; } = [];
        public List<CaseDto> SearchResults { get; set; } = [];
        public string? LastSearchTerm { get; private set; }
        public Guid? LastDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/api/cases/search"))
            {
                var uri = request.RequestUri!;
                var queryParams = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.OrdinalIgnoreCase);

                queryParams.TryGetValue("term", out var term);
                LastSearchTerm = term;

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, SearchResults));
            }

            if (request.Method == HttpMethod.Get && (path.EndsWith("/api/cases") || path.EndsWith("/api/cases/")))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Cases));
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/api/cases/"))
            {
                var parts = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && Guid.TryParse(parts[^1], out var id))
                {
                    LastDeletedId = id;
                    Cases = Cases.Where(c => c.Id != id).ToList();
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
        }
    }
}
