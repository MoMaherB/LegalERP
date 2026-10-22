using Bunit;
using FluentAssertions;
using LegalERP.Application.Cases;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Cases.Sections;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CaseHearingsSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly RecordingHttpMessageHandler _handler = new();

    public CaseHearingsSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
    }

    private static CaseDto CreateCase(List<CaseHearingDto>? hearings = null) =>
        new(
            Id: Guid.NewGuid(),
            CaseNumber: "C-2026-300",
            Title: "Contract Hearing Case",
            CaseType: CaseType.Civil,
            Status: CaseStatus.Active,
            Outcome: null,
            FilingDate: new DateOnly(2026, 2, 1),
            CourtName: "Cairo Court",
            JudgeName: null,
            Notes: null,
            Parties: new List<CasePartyDto>(),
            Memos: new List<CaseMemoDto>(),
            Hearings: hearings ?? new List<CaseHearingDto>()
        );

    [Fact]
    public void CaseHearingsSection_WhenNoHearingsExist_RendersNoHearingsAlertAndRespectsCanEdit()
    {
        var caseDto = CreateCase();

        var cutReadOnly = RenderComponent<CaseHearingsSection>(p => p
            .Add(x => x.Case, caseDto)
            .Add(x => x.CanEdit, false)
            .Add(x => x.CanDelete, false));

        cutReadOnly.Find(".alert").TextContent.Should().Contain("NoHearings");
        cutReadOnly.FindAll("button.ios-btn-primary").Should().BeEmpty();

        var cutEditable = RenderComponent<CaseHearingsSection>(p => p
            .Add(x => x.Case, caseDto)
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, false));

        cutEditable.Find("button.ios-btn-primary").TextContent.Should().Contain("AddHearing");
    }

    [Fact]
    public void CaseHearingsSection_WhenHearingsExist_RendersChronologicalRowsAndRespectsCanDelete()
    {
        var hearings = new List<CaseHearingDto>
        {
            new(Guid.NewGuid(), new DateOnly(2026, 3, 1), "First Session", "Adjourned", "Notify defendant"),
            new(Guid.NewGuid(), new DateOnly(2026, 4, 15), "Final Pleading", "Reserved for judgment", null)
        };
        var caseDto = CreateCase(hearings);

        var cut = RenderComponent<CaseHearingsSection>(p => p
            .Add(x => x.Case, caseDto)
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, false));

        var rows = cut.FindAll("tbody tr");
        rows.Should().HaveCount(2);
        rows[0].TextContent.Should().Contain("2026-04-15").And.Contain("Final Pleading");
        rows[1].TextContent.Should().Contain("2026-03-01").And.Contain("First Session");
        cut.FindAll("button.ios-btn-danger").Should().BeEmpty();
    }

    [Fact]
    public void CaseHearingsSection_AddingNewHearing_PostsHearingAndInvokesOnChanged()
    {
        var caseDto = CreateCase();
        var changedCount = 0;

        var cut = RenderComponent<CaseHearingsSection>(p => p
            .Add(x => x.Case, caseDto)
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true)
            .Add(x => x.OnChanged, () => changedCount++));

        cut.Find("button.ios-btn-primary").Click();
        cut.Find("input[placeholder='HearingPurpose']").Change("Expert Report Discussion");
        cut.Find("input[placeholder='HearingDecision']").Change("Postponed");
        cut.Find("input[placeholder='HearingPostponement']").Change("Await expert attendance");

        var saveBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("SaveHearing"));
        saveBtn.Click();

        _handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Post &&
            r.Uri.EndsWith($"api/cases/{caseDto.Id}/hearings") &&
            r.Body != null &&
            r.Body.Contains("Expert Report Discussion"));
        changedCount.Should().Be(1);
    }

    [Fact]
    public void CaseHearingsSection_EditingExistingHearing_PutsUpdatedHearingAndInvokesOnChanged()
    {
        var hearingId = Guid.NewGuid();
        var hearings = new List<CaseHearingDto>
        {
            new(hearingId, new DateOnly(2026, 5, 1), "Initial Purpose", "Initial Decision", "Initial Reason")
        };
        var caseDto = CreateCase(hearings);
        var changedCount = 0;

        var cut = RenderComponent<CaseHearingsSection>(p => p
            .Add(x => x.Case, caseDto)
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true)
            .Add(x => x.OnChanged, () => changedCount++));

        var editBtn = cut.FindAll("button.ios-btn-secondary")
            .First(b => b.TextContent.Contains("Edit"));
        editBtn.Click();

        cut.Find("input[placeholder='HearingDecision']").Change("Final Verdict Issued");

        var saveBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("SaveHearing"));
        saveBtn.Click();

        _handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Put &&
            r.Uri.EndsWith($"api/cases/{caseDto.Id}/hearings/{hearingId}") &&
            r.Body != null &&
            r.Body.Contains("Final Verdict Issued"));
        changedCount.Should().Be(1);
    }

    [Fact]
    public void CaseHearingsSection_DeletingHearing_ConfirmsDeletesAndInvokesOnChanged()
    {
        var hearingId = Guid.NewGuid();
        var hearings = new List<CaseHearingDto>
        {
            new(hearingId, new DateOnly(2026, 5, 1), "Session to Delete", "Adjourned", null)
        };
        var caseDto = CreateCase(hearings);
        var changedCount = 0;
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<CaseHearingsSection>(p => p
            .Add(x => x.Case, caseDto)
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true)
            .Add(x => x.OnChanged, () => changedCount++));

        cut.Find("button.ios-btn-danger").Click();

        _handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Delete &&
            r.Uri.EndsWith($"api/cases/{caseDto.Id}/hearings/{hearingId}"));
        changedCount.Should().Be(1);
    }

    private class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public List<(HttpMethod Method, string Uri, string? Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            var body = request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : null;
            Requests.Add((request.Method, uri, body));

            if (request.Method == HttpMethod.Post && uri.Contains("/hearings"))
            {
                var created = new CaseHearingDto(
                    Guid.NewGuid(),
                    new DateOnly(2026, 5, 10),
                    "Expert Report Discussion",
                    "Postponed",
                    "Await expert attendance");
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(JsonSerializer.Serialize(created, JsonOptions), System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
