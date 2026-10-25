using Bunit;
using FluentAssertions;
using LegalERP.Application.Clients;
using LegalERP.Web;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientQuickSearchSelectorTests : TestContext
{
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly StubHttpMessageHandler _httpHandler = new();

    public ClientQuickSearchSelectorTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public async Task Search_WhenExcludedClientIdsProvided_MarksExcludedClientsAsAlreadyLinkedAndDisabled()
    {
        var linkedClientId = Guid.NewGuid();
        var availableClientId = Guid.NewGuid();
        _httpHandler.SearchResults =
        [
            new ClientSummaryDto(linkedClientId, "C-1", "أحمد مرتبط", "29001011111111", "01011111111"),
            new ClientSummaryDto(availableClientId, "C-2", "أحمد متاح", "29001022222222", "01022222222")
        ];

        ClientSummaryDto? selected = null;
        var cut = RenderComponent<ClientQuickSearchSelector>(parameters => parameters
            .Add(p => p.DebounceMs, 0)
            .Add(p => p.ExcludedClientIds, new[] { linkedClientId })
            .Add(p => p.SelectedClientChanged, c => selected = c));

        var input = cut.Find("input.ios-input");
        input.Input("أحمد");
        await input.KeyUpAsync(new KeyboardEventArgs { Key = "د" });

        var buttons = cut.FindAll("button.list-group-item");
        buttons.Should().HaveCount(2);

        buttons[0].TextContent.Should().Contain("أحمد مرتبط");
        buttons[0].TextContent.Should().Contain("AlreadyLinked");
        buttons[0].HasAttribute("disabled").Should().BeTrue();

        buttons[1].TextContent.Should().Contain("أحمد متاح");
        buttons[1].TextContent.Should().NotContain("AlreadyLinked");
        buttons[1].HasAttribute("disabled").Should().BeFalse();

        // Clicking excluded client should not select it
        buttons[0].Click();
        selected.Should().BeNull();

        // Clicking available client selects it
        cut.FindAll("button.list-group-item")[1].Click();
        selected.Should().NotBeNull();
        selected!.Id.Should().Be(availableClientId);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public List<ClientSummaryDto> SearchResults { get; set; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(SearchResults);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
