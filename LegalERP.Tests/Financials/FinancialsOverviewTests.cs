using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Financials;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Financials;
using LegalERP.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace LegalERP.Tests.Financials;

public class FinancialsOverviewTests : TestContext
{
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeSummaryHandler _handler = new();

    public FinancialsOverviewTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("AdminUser");
        auth.SetRoles("Admin");

        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new FinancialsApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void FinancialsOverview_WhenLoaded_RendersSummaryCardsAndTableRecords()
    {
        var caseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var summary = new GlobalFinancialsDto
        {
            TotalCaseAgreedFees = 10000m,
            TotalCaseCollected = 6000m,
            TotalCompanyAgreedFees = 20000m,
            TotalCompanyCollected = 15000m,
            Records = new List<FinancialRecordDto>
            {
                new()
                {
                    OwnerId = caseId,
                    OwnerType = "Case",
                    OwnerName = "Alpha Case",
                    ReferenceNumber = "CASE-111",
                    AgreedFee = 10000m,
                    TotalCollected = 6000m,
                    PaymentStatus = PaymentStatus.PartiallyPaid
                },
                new()
                {
                    OwnerId = companyId,
                    OwnerType = "Company",
                    OwnerName = "Beta Corp",
                    ReferenceNumber = "CR-222",
                    AgreedFee = 20000m,
                    TotalCollected = 15000m,
                    PaymentStatus = PaymentStatus.PartiallyPaid
                }
            }
        };

        _handler.Summary = summary;

        var cut = RenderComponent<FinancialsOverview>();

        cut.Find("h2.ios-title").TextContent.Should().Contain("FirmFinancialsOverview");
        cut.Markup.Should().Contain("Alpha Case").And.Contain("CASE-111");
        cut.Markup.Should().Contain("Beta Corp").And.Contain("CR-222");
        cut.Markup.Should().Contain("10,000").And.Contain("6,000");
        cut.Markup.Should().NotContain("10,000.00").And.NotContain("10,000,00");

        var viewLinks = cut.FindAll("a.ios-btn-secondary");
        viewLinks.Should().Contain(a => a.GetAttribute("href") == $"/cases/{caseId}");
        viewLinks.Should().Contain(a => a.GetAttribute("href") == $"/companies/{companyId}");
    }

    private class FakeSummaryHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public GlobalFinancialsDto? Summary { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = JsonSerializer.Serialize(Summary, JsonOptions);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
