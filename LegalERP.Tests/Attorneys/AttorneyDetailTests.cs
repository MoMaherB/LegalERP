using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Attorneys;
using LegalERP.Application.Clients;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Attorneys;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace LegalERP.Tests.Attorneys;

public class AttorneyDetailTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly RecordingHttpMessageHandler _httpHandler = new();

    public AttorneyDetailTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("TestEditor");
        auth.SetRoles("Editor");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new AttorneyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new DocumentApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public async Task AddClient_UsesClientQuickSearchSelectorWithExcludedIdsAndLinksSelectedClient()
    {
        var attorneyId = Guid.NewGuid();
        var existingClientId = Guid.NewGuid();
        var newClientId = Guid.NewGuid();

        _httpHandler.Attorney = new AttorneyDto(
            Id: attorneyId,
            AttorneyNumber: "ATT-2026-10",
            Notes: "توكيل عام",
            Document: null,
            Clients:
            [
                new AttorneyClientDto(Guid.NewGuid(), existingClientId, "موكل حالي", "29001011111111", "F-1")
            ]);

        _httpHandler.SearchResults =
        [
            new ClientSummaryDto(existingClientId, "F-1", "موكل حالي", "29001011111111", "01011111111"),
            new ClientSummaryDto(newClientId, "F-2", "موكل جديد", "29001022222222", "01022222222")
        ];

        var cut = RenderComponent<AttorneyDetail>(parameters => parameters
            .Add(p => p.Id, attorneyId));

        // Open Add Client form
        var addBtn = cut.FindAll("button.ios-btn-primary").First(b => b.TextContent.Contains("AddClient"));
        addBtn.Click();

        // Verify ClientQuickSearchSelector is rendered with existingClientId excluded
        var selector = cut.FindComponent<ClientQuickSearchSelector>();
        selector.Instance.ExcludedClientIds.Should().NotBeNull();
        selector.Instance.ExcludedClientIds.Should().Contain(existingClientId);

        // Search inside ClientQuickSearchSelector
        var searchInput = selector.Find("input.ios-input");
        searchInput.Input("موكل");
        await searchInput.KeyUpAsync(new KeyboardEventArgs { Key = "ل" });

        cut.WaitForAssertion(() => selector.FindAll("button.list-group-item").Should().HaveCount(2), TimeSpan.FromSeconds(2));

        // Select the available client (index 1)
        selector.FindAll("button.list-group-item")[1].Click();

        // Click the Link button
        var linkBtn = cut.FindAll("button.ios-btn-primary").First(b => b.TextContent.Contains("Link"));
        await cut.InvokeAsync(() => linkBtn.Click());

        _httpHandler.LastLinkedClient.Should().NotBeNull();
        _httpHandler.LastLinkedClient!.ClientId.Should().Be(newClientId);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public AttorneyDto? Attorney { get; set; }
        public List<ClientSummaryDto> SearchResults { get; set; } = [];
        public AddAttorneyClientDto? LastLinkedClient { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/attorneys/", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Attorney, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/clients/search", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(SearchResults, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/clients", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastLinkedClient = JsonSerializer.Deserialize<AddAttorneyClientDto>(body, JsonOptions);

                var createdJoin = new AttorneyClientDto(
                    Guid.NewGuid(),
                    LastLinkedClient?.ClientId ?? Guid.Empty,
                    "موكل جديد",
                    "29001022222222",
                    "F-2");

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(createdJoin, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
