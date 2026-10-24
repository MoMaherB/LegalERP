using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Cases;
using LegalERP.Application.Financials;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Cases;
using LegalERP.Web.Components.Pages.Cases.Sections;
using LegalERP.Web.Components.Shared;
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

public class CaseDetailOrchestratorTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeHttpMessageHandler _handler = new();

    public CaseDetailOrchestratorTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("TestAdmin");
        auth.SetRoles("Admin");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new FinancialsApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void CaseDetail_WhenLoaded_OrchestratesAllChildSectionsIncludingFinancials()
    {
        var caseId = Guid.NewGuid();
        var caseDto = new CaseDto(
            Id: caseId,
            CaseNumber: "C-2026-999",
            Title: "Landmark Commercial Dispute",
            CaseType: CaseType.Civil,
            Status: CaseStatus.Active,
            Outcome: null,
            FilingDate: new DateOnly(2026, 1, 10),
            CourtName: "Cairo Economic Court",
            JudgeName: "Judge Hassan",
            Notes: "Important commercial dispute",
            Parties: new List<CasePartyDto>(),
            Memos: new List<CaseMemoDto>(),
            Hearings: new List<CaseHearingDto>()
        );

        _handler.SetCase(caseDto);

        var cut = RenderComponent<CaseDetail>(parameters => parameters
            .Add(p => p.Id, caseId));

        cut.Find("h3.ios-title").TextContent.Should().Contain("Landmark Commercial Dispute");

        cut.FindComponents<CasePartiesSection>().Should().HaveCount(1);
        cut.FindComponents<CaseMemosSection>().Should().HaveCount(1);
        cut.FindComponents<CaseHearingsSection>().Should().HaveCount(1);
        cut.FindComponents<EntityDocumentsSection>().Should().HaveCount(1);
        cut.FindComponents<SharedFinancials>().Should().HaveCount(1);
    }

    [Fact]
    public void CaseDetail_ActionButtons_AreAlignedInSingleHorizontalFlexContainer()
    {
        var caseId = Guid.NewGuid();
        var caseDto = new CaseDto(
            Id: caseId,
            CaseNumber: "C-2026-999",
            Title: "Sample Case",
            CaseType: CaseType.Civil,
            Status: CaseStatus.Active,
            Outcome: null,
            FilingDate: new DateOnly(2026, 1, 10),
            CourtName: "Cairo Court",
            JudgeName: "Judge Hassan",
            Notes: null,
            Parties: new List<CasePartyDto>(),
            Memos: new List<CaseMemoDto>(),
            Hearings: new List<CaseHearingDto>()
        );

        _handler.SetCase(caseDto);

        var cut = RenderComponent<CaseDetail>(parameters => parameters
            .Add(p => p.Id, caseId));

        var actionsContainer = cut.Find("div.d-flex.align-items-center.gap-2");
        actionsContainer.Should().NotBeNull();
        actionsContainer.QuerySelector("a.ios-btn-secondary").Should().NotBeNull();
        actionsContainer.QuerySelector("a.ios-btn-primary").Should().NotBeNull();
        actionsContainer.QuerySelector("button.ios-btn-danger").Should().NotBeNull();
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private string _caseJson = "{}";

        public void SetCase(CaseDto caseDto)
        {
            _caseJson = JsonSerializer.Serialize(caseDto, JsonOptions);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            string content;
            if (uri.Contains("/financials"))
            {
                var financials = new EntityFinancialsDto(0m, 0m, 0m, PaymentStatus.Unpaid, new List<FeeTransactionDto>());
                content = JsonSerializer.Serialize(financials, JsonOptions);
            }
            else if (uri.Contains("api/cases/"))
            {
                content = _caseJson;
            }
            else
            {
                content = "[]";
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
