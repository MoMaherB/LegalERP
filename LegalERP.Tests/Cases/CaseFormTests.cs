using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
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

public class CaseFormTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly CaseFormRecordingHandler _handler = new();

    public CaseFormTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void CaseForm_CreateMode_RendersInitialFormWithDefaults()
    {
        var cut = RenderComponent<CaseForm>();

        cut.Find("h1.ios-title").TextContent.Should().Be("AddNewCase");
        cut.Find("button[type='submit']").TextContent.Should().Contain("CreateCase");
    }

    [Fact]
    public async Task CaseForm_CreateMode_WhenRequiredFieldsEmpty_ShowsWarningToast()
    {
        var cut = RenderComponent<CaseForm>();

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _mockToast.Verify(t => t.ShowWarning("Case Number and Title are required."), Times.Once);
        _handler.LastCreatedCase.Should().BeNull();
    }

    [Fact]
    public async Task CaseForm_CreateMode_WhenStatusIsActive_ClearsOutcomeOnSubmit()
    {
        var cut = RenderComponent<CaseForm>();

        cut.Find("input[placeholder='CaseNumberPlaceholder']").Change("CASE-ACTIVE-01");
        cut.Find("input[placeholder='TitleSubjectPlaceholder']").Change("دعوى مدنية نشطة");

        // Status defaults to Active; even if an Outcome was set, Active cases must clear it
        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.LastCreatedCase.Should().NotBeNull();
        _handler.LastCreatedCase!.CaseNumber.Should().Be("CASE-ACTIVE-01");
        _handler.LastCreatedCase.Title.Should().Be("دعوى مدنية نشطة");
        _handler.LastCreatedCase.Status.Should().Be(CaseStatus.Active);
        _handler.LastCreatedCase.Outcome.Should().BeNull();
    }

    [Fact]
    public async Task CaseForm_CreateMode_WhenStatusIsClosed_SubmitsWithSelectedOutcome()
    {
        var cut = RenderComponent<CaseForm>();

        cut.Find("input[placeholder='CaseNumberPlaceholder']").Change("CASE-CLOSED-01");
        cut.Find("input[placeholder='TitleSubjectPlaceholder']").Change("دعوى مغلقة مع كسب الحكم");

        // Change Status to Closed
        var statusSelect = cut.FindAll("select.ios-select").First(s => s.QuerySelector("option[value='Active']") != null);
        statusSelect.Change(CaseStatus.Closed.ToString());

        cut.WaitForAssertion(() => cut.FindAll("select.ios-select").Should().HaveCount(3));

        // Select Outcome = Won
        var outcomeSelect = cut.FindAll("select.ios-select").First(s => s.QuerySelector("option[value='Won']") != null);
        outcomeSelect.Change(CaseOutcome.Won.ToString());

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() =>
        {
            _handler.LastCreatedCase.Should().NotBeNull();
            _handler.LastCreatedCase!.Status.Should().Be(CaseStatus.Closed);
            _handler.LastCreatedCase.Outcome.Should().Be(CaseOutcome.Won);
        });
    }

    [Fact]
    public void CaseForm_EditMode_LoadsExistingCaseAndPopulatesFields()
    {
        var caseId = Guid.NewGuid();
        _handler.ExistingCase = CreateCaseDto(
            id: caseId,
            caseNumber: "CASE-EDIT-100",
            title: "قضية مدنية منظورة",
            caseType: CaseType.Civil,
            status: CaseStatus.Active,
            courtName: "محكمة القاهرة"
        );

        var cut = RenderComponent<CaseForm>(p => p.Add(x => x.Id, caseId));

        cut.Find("h1.ios-title").TextContent.Should().Be("EditCase");
        cut.Find("input[placeholder='CaseNumberPlaceholder']").GetAttribute("value").Should().Be("CASE-EDIT-100");
        cut.Find("input[placeholder='TitleSubjectPlaceholder']").GetAttribute("value").Should().Be("قضية مدنية منظورة");
        cut.Find("button[type='submit']").TextContent.Should().Contain("SaveChanges");
    }

    [Fact]
    public async Task CaseForm_EditMode_WhenSwitchingFromClosedToActive_ClearsOutcome()
    {
        var caseId = Guid.NewGuid();
        _handler.ExistingCase = CreateCaseDto(
            id: caseId,
            caseNumber: "CASE-REOPEN-99",
            title: "قضية معاد فتحها",
            caseType: CaseType.Civil,
            status: CaseStatus.Closed,
            outcome: CaseOutcome.Lost
        );

        var cut = RenderComponent<CaseForm>(p => p.Add(x => x.Id, caseId));

        // Switch Status to Active
        var statusSelect = cut.FindAll("select.ios-select").First(s => s.QuerySelector("option[value='Active']") != null);
        statusSelect.Change(CaseStatus.Active.ToString());

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() =>
        {
            _handler.LastUpdatedCaseId.Should().Be(caseId);
            _handler.LastUpdatedCase.Should().NotBeNull();
            _handler.LastUpdatedCase!.Status.Should().Be(CaseStatus.Active);
            _handler.LastUpdatedCase.Outcome.Should().BeNull();
        });
    }

    [Fact]
    public async Task CaseForm_EditMode_WhenPreservingClosed_SendsUpdatedOutcome()
    {
        var caseId = Guid.NewGuid();
        _handler.ExistingCase = CreateCaseDto(
            id: caseId,
            caseNumber: "CASE-CHANGE-OUTCOME",
            title: "قضية تم التوصل لتصالح بها",
            caseType: CaseType.Civil,
            status: CaseStatus.Closed,
            outcome: CaseOutcome.Won
        );

        var cut = RenderComponent<CaseForm>(p => p.Add(x => x.Id, caseId));

        // Change Outcome from Won to Settled
        var outcomeSelect = cut.FindAll("select.ios-select").First(s => s.QuerySelector("option[value='Won']") != null);
        outcomeSelect.Change(CaseOutcome.Settled.ToString());

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.LastUpdatedCaseId.Should().Be(caseId);
        _handler.LastUpdatedCase.Should().NotBeNull();
        _handler.LastUpdatedCase!.Status.Should().Be(CaseStatus.Closed);
        _handler.LastUpdatedCase.Outcome.Should().Be(CaseOutcome.Settled);
    }

    [Fact]
    public void CaseForm_EditMode_WhenCaseNotFound_ShowsErrorToast()
    {
        var caseId = Guid.NewGuid();
        _handler.ExistingCase = null;

        var cut = RenderComponent<CaseForm>(p => p.Add(x => x.Id, caseId));

        _mockToast.Verify(t => t.ShowError("Case not found."), Times.Once);
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

    private class CaseFormRecordingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public Guid CreatedCaseId { get; } = Guid.NewGuid();
        public CaseDto? ExistingCase { get; set; }

        public CreateCaseDto? LastCreatedCase { get; private set; }
        public Guid? LastUpdatedCaseId { get; private set; }
        public UpdateCaseDto? LastUpdatedCase { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/cases/", StringComparison.OrdinalIgnoreCase))
            {
                if (ExistingCase == null)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ExistingCase, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.Equals("/api/cases", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastCreatedCase = JsonSerializer.Deserialize<CreateCaseDto>(body, JsonOptions);

                var createdDto = CreateCaseDto(
                    id: CreatedCaseId,
                    caseNumber: LastCreatedCase?.CaseNumber ?? string.Empty,
                    title: LastCreatedCase?.Title ?? string.Empty,
                    caseType: LastCreatedCase?.CaseType ?? CaseType.Civil,
                    status: LastCreatedCase?.Status ?? CaseStatus.Active,
                    outcome: LastCreatedCase?.Outcome,
                    courtName: LastCreatedCase?.CourtName
                );

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(createdDto, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Put && path.StartsWith("/api/cases/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var id))
                    LastUpdatedCaseId = id;

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastUpdatedCase = JsonSerializer.Deserialize<UpdateCaseDto>(body, JsonOptions);

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
