using Bunit;
using FluentAssertions;
using LegalERP.Application.Cases;
using LegalERP.Application.Clients;
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
using Xunit;

namespace LegalERP.Tests.Cases;

public class CasePartiesSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly RecordingHttpMessageHandler _httpHandler = new();

    public CasePartiesSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
    }

    private static CaseDto CreateCase(
        CaseType caseType = CaseType.Criminal,
        List<CasePartyDto>? parties = null)
    {
        return new CaseDto(
            Id: Guid.NewGuid(),
            CaseNumber: "2026/500",
            Title: "قضية جنائية",
            CaseType: caseType,
            Status: CaseStatus.Active,
            Outcome: null,
            FilingDate: new DateOnly(2026, 1, 15),
            CourtName: "محكمة شمال القاهرة",
            JudgeName: null,
            Notes: null,
            Parties: parties ?? new List<CasePartyDto>(),
            Memos: new List<CaseMemoDto>(),
            Hearings: new List<CaseHearingDto>()
        );
    }

    [Fact]
    public void CasePartiesSection_WhenNoPartiesExist_RendersEmptyStateAlert()
    {
        var caseDto = CreateCase(parties: new List<CasePartyDto>());

        var cut = RenderComponent<CasePartiesSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        cut.Find(".alert").TextContent.Should().Contain("NoPartiesRecorded");
    }

    [Fact]
    public void CasePartiesSection_WhenPartiesExist_RendersRoleBadgesAndClientVsOpponentRepresentation()
    {
        var clientId = Guid.NewGuid();
        var parties = new List<CasePartyDto>
        {
            new(Guid.NewGuid(), clientId, "أحمد محمود", PartyRole.Defendant, true, "29001011234567", "01012345678", null, null, null),
            new(Guid.NewGuid(), null, "خصم يدوي", PartyRole.Victim, false, null, null, null, null, null)
        };
        var caseDto = CreateCase(CaseType.Criminal, parties);

        var cut = RenderComponent<CasePartiesSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
        );

        var rows = cut.FindAll("tbody tr");
        rows.Should().HaveCount(2);

        rows[0].TextContent.Should().Contain("أحمد محمود");
        rows[0].TextContent.Should().Contain("DefendantCriminal");
        rows[0].TextContent.Should().Contain("OurClient");
        rows[0].TextContent.Should().Contain("29001011234567");
        rows[0].TextContent.Should().Contain("01012345678");

        rows[1].TextContent.Should().Contain("خصم يدوي");
        rows[1].TextContent.Should().Contain("VictimCriminal");
        rows[1].TextContent.Should().Contain("Opponent");
    }

    [Fact]
    public void CasePartiesSection_WhenCanEditAndCanDeleteAreFalse_HidesAddAndDeleteButtons()
    {
        var parties = new List<CasePartyDto>
        {
            new(Guid.NewGuid(), null, "خصم يدوي", PartyRole.Defendant, false, null, null, null, null, null)
        };
        var caseDto = CreateCase(CaseType.Civil, parties);

        var cut = RenderComponent<CasePartiesSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        cut.FindAll("button.ios-btn-primary").Should().BeEmpty();
        cut.FindAll("button.ios-btn-danger").Should().BeEmpty();
        cut.Find("tbody tr").TextContent.Should().Contain("DefendantCivil");
    }

    [Fact]
    public async Task CasePartiesSection_WhenAddingOurClientParty_SearchesSelectsAndSubmitsCleanFullName()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.SearchResults = new List<ClientSummaryDto>
        {
            new(clientId, "CL-01", "أحمد محمود", "29001011234567", "01012345678")
        };

        var caseDto = CreateCase(CaseType.Civil);
        var onChangedCalled = false;

        var cut = RenderComponent<CasePartiesSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
            .Add(p => p.OnChanged, () => { onChangedCalled = true; })
        );

        // Open Add Party form
        cut.Find("button.ios-btn-primary").Click();

        // Type in ClientQuickSearchSelector input and trigger keyup
        var searchInput = cut.Find("input[placeholder='TypeClientNamePlaceholder'], input[placeholder='TypeClientNameSearch']");
        searchInput.Input("أحمد");
        await searchInput.KeyUpAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "د" });

        // Wait for debounce and select client from dropdown
        cut.WaitForAssertion(() => cut.FindAll("button.list-group-item-action").Should().NotBeEmpty(), TimeSpan.FromSeconds(2));
        cut.Find("button.list-group-item-action").Click();

        // Click SaveParty button
        var saveBtn = cut.FindAll("button.ios-btn-primary").First(b => b.TextContent.Contains("SaveParty"));
        await cut.InvokeAsync(() => saveBtn.Click());

        _httpHandler.LastCreatedParty.Should().NotBeNull();
        _httpHandler.LastCreatedParty!.ClientId.Should().Be(clientId);
        _httpHandler.LastCreatedParty.FullName.Should().Be("أحمد محمود");
        _httpHandler.LastCreatedParty.IsOurClient.Should().BeTrue();
        onChangedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CasePartiesSection_WhenAddingOpponentParty_HidesClientSelectorAndSubmitsManualName()
    {
        var caseDto = CreateCase(CaseType.Civil);

        var cut = RenderComponent<CasePartiesSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
        );

        // Open Add Party form
        cut.Find("button.ios-btn-primary").Click();

        // Switch ClientStatus select to "opponent"
        var selects = cut.FindAll("select.ios-select");
        selects[1].Change("opponent");

        // Enter manual opponent name
        var nameInput = cut.Find("input[placeholder='PartyName']");
        nameInput.Change("شركة الخصم للتجارة");

        // Click SaveParty
        var saveBtn = cut.FindAll("button.ios-btn-primary").First(b => b.TextContent.Contains("SaveParty"));
        await cut.InvokeAsync(() => saveBtn.Click());

        _httpHandler.LastCreatedParty.Should().NotBeNull();
        _httpHandler.LastCreatedParty!.ClientId.Should().BeNull();
        _httpHandler.LastCreatedParty.FullName.Should().Be("شركة الخصم للتجارة");
        _httpHandler.LastCreatedParty.IsOurClient.Should().BeFalse();
    }

    private class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public List<ClientSummaryDto> SearchResults { get; set; } = new();
        public CreateCasePartyDto? LastCreatedParty { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/clients/search", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(SearchResults, JsonOptions), System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/parties", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastCreatedParty = JsonSerializer.Deserialize<CreateCasePartyDto>(body, JsonOptions);

                var responseDto = new CasePartyDto(
                    Guid.NewGuid(),
                    LastCreatedParty?.ClientId,
                    LastCreatedParty?.FullName ?? string.Empty,
                    LastCreatedParty?.Role ?? PartyRole.Defendant,
                    LastCreatedParty?.IsOurClient ?? true,
                    null,
                    null,
                    LastCreatedParty?.Notes,
                    null,
                    null
                );

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseDto, JsonOptions), System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
