using Bunit;
using FluentAssertions;
using LegalERP.Application.Financials;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace LegalERP.Tests.Financials;

public class SharedFinancialsTests : TestContext
{
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly RecordingHttpMessageHandler _handler = new();

    public SharedFinancialsTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new FinancialsApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
    }

    [Theory]
    [InlineData("case")]
    [InlineData("company")]
    public void SharedFinancials_OnLoad_FetchesLedgerFromUnifiedFinancialsEndpoint(string ownerType)
    {
        var ownerId = Guid.NewGuid();
        _handler.Financials = new EntityFinancialsDto(3000m, 1000m, 2000m, PaymentStatus.PartiallyPaid, new List<FeeTransactionDto>());

        var cut = RenderComponent<SharedFinancials>(p => p
            .Add(x => x.OwnerType, ownerType)
            .Add(x => x.OwnerId, ownerId));

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Get &&
            r.Uri.EndsWith($"api/financials/{ownerType}/{ownerId}"));
        cut.Markup.Should().Contain("3,000").And.Contain("1,000").And.Contain("2,000");
        cut.Markup.Should().NotContain("3,000.00").And.NotContain("3,000,00");
        cut.Markup.Should().Contain("PartiallyPaid");
    }

    [Fact]
    public void SharedFinancials_WhenOvercollected_RendersWholeCurrencyWithoutCentsOrNegativeRemaining()
    {
        // User symptom: 50,000 agreed, 60,000 collected -> displays 50,000 and 60,000 (no cents) and 0 remaining
        var caseId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        _handler.Financials = new EntityFinancialsDto(
            50000m,
            60000m,
            0m,
            PaymentStatus.Paid,
            new List<FeeTransactionDto>
            {
                new(txId, 60000m, new DateOnly(2026, 10, 23), "RC-99", null)
            });

        var cut = RenderComponent<SharedFinancials>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId));

        cut.Markup.Should().Contain("50,000");
        cut.Markup.Should().Contain("60,000");
        cut.Markup.Should().NotContain("50,000.00").And.NotContain("50,000,00");
        cut.Markup.Should().NotContain("60,000.00").And.NotContain("60,000,00");
        cut.Markup.Should().NotContain("-10,000");
    }

    [Fact]
    public void SharedFinancials_AddingTransaction_PostsToUnifiedTransactionsEndpoint()
    {
        var companyId = Guid.NewGuid();
        _handler.Financials = new EntityFinancialsDto(5000m, 0m, 5000m, PaymentStatus.Unpaid, new List<FeeTransactionDto>());

        var cut = RenderComponent<SharedFinancials>(p => p
            .Add(x => x.OwnerType, "company")
            .Add(x => x.OwnerId, companyId));

        cut.Find("button.btn-primary").Click();
        cut.Find("input[type=number]").Change("1500");
        cut.Find("button.btn-success").Click();

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Post &&
            r.Uri.EndsWith($"api/financials/company/{companyId}/transactions") &&
            r.Body != null && r.Body.Contains("1500"));
    }

    [Fact]
    public void SharedFinancials_DeletingTransaction_WhenConfirmed_CallsUnifiedDeleteEndpoint()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var caseId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _handler.Financials = new EntityFinancialsDto(
            2000m, 500m, 1500m, PaymentStatus.PartiallyPaid,
            new List<FeeTransactionDto> { new(transactionId, 500m, new DateOnly(2026, 5, 1), "RC-1", null) });

        var cut = RenderComponent<SharedFinancials>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId));

        cut.Find("button.btn-outline-danger").Click();

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete &&
            r.Uri.EndsWith($"api/financials/case/{caseId}/transactions/{transactionId}"));
    }

    [Fact]
    public void SharedFinancials_DeletingTransaction_WhenCancelled_DoesNotCallDeleteEndpoint()
    {
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var caseId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        _handler.Financials = new EntityFinancialsDto(
            2000m, 500m, 1500m, PaymentStatus.PartiallyPaid,
            new List<FeeTransactionDto> { new(transactionId, 500m, new DateOnly(2026, 5, 1), "RC-1", null) });

        var cut = RenderComponent<SharedFinancials>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId));

        cut.Find("button.btn-outline-danger").Click();

        _handler.Requests.Should().NotContain(r =>
            r.Method == HttpMethod.Delete);
    }

    [Fact]
    public void SharedFinancials_SavingAgreedFee_PutsToUnifiedAgreedFeeEndpoint()
    {
        var caseId = Guid.NewGuid();
        _handler.Financials = new EntityFinancialsDto(null, 0m, 0m, PaymentStatus.Unpaid, new List<FeeTransactionDto>());

        var cut = RenderComponent<SharedFinancials>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId));

        cut.Find("button.btn-outline-secondary").Click();
        cut.Find("input[type=number]").Change("9000");
        cut.Find("button.btn-success").Click();

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Put &&
            r.Uri.EndsWith($"api/financials/case/{caseId}/agreed-fee") &&
            r.Body != null && r.Body.Contains("9000"));
    }

    private class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public EntityFinancialsDto Financials { get; set; } =
            new(null, 0m, 0m, PaymentStatus.Unpaid, new List<FeeTransactionDto>());

        public List<(HttpMethod Method, string Uri, string? Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : null;
            Requests.Add((request.Method, uri, body));

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Financials, JsonOptions), System.Text.Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post)
            {
                var created = new FeeTransactionDto(Guid.NewGuid(), 1500m, new DateOnly(2026, 7, 1), null, null);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(created, JsonOptions), System.Text.Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
