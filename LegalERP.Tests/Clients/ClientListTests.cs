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
using LegalERP.Application.Clients;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Clients;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientListTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeClientListHttpMessageHandler _httpHandler = new();

    public ClientListTests()
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
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void ClientList_WhenLoaded_RendersClientTableRows()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(clientId, "CL-555", "عادل إمام", "27001011234567", "01009988776")
        };

        var cut = RenderComponent<ClientList>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(1);
            var rowText = rows[0].TextContent;
            rowText.Should().Contain("CL-555");
            rowText.Should().Contain("عادل إمام");
            rowText.Should().Contain("27001011234567");
            rowText.Should().Contain("01009988776");
        });
    }

    [Fact]
    public void ClientList_WhenEmpty_RendersNoClientsFoundMessage()
    {
        _httpHandler.Clients = new List<ClientSummaryDto>();

        var cut = RenderComponent<ClientList>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".alert-info").TextContent.Should().Contain("NoClientsFound");
        });
    }

    [Fact]
    public async Task ClientList_Search_CallsSearchApiAndUpdatesList()
    {
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-1", "سامي فهمي", null, null)
        };
        _httpHandler.SearchResults = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-2", "سامح عبد الشكور", "29505051234567", null)
        };

        var cut = RenderComponent<ClientList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("سامح");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("سامح");
            cut.Find("tbody tr").TextContent.Should().Contain("سامح عبد الشكور");
        });
    }

    [Fact]
    public async Task ClientList_Reset_ClearsSearchTermAndReloadsAllClients()
    {
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-ALL", "عميل عام", null, null)
        };
        _httpHandler.SearchResults = new List<ClientSummaryDto>
        {
            new(Guid.NewGuid(), "CL-SEARCH", "عميل بحث", null, null)
        };

        var cut = RenderComponent<ClientList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("بحث");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("عميل بحث");
        });

        var resetBtn = cut.Find("button.ios-btn-secondary");
        await cut.InvokeAsync(() => resetBtn.Click());

        cut.WaitForAssertion(() =>
        {
            cut.Find("tbody tr").TextContent.Should().Contain("عميل عام");
        });
    }

    [Fact]
    public async Task ClientList_DeleteClient_WhenConfirmed_CallsDeleteApiAndRefreshes()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.Clients = new List<ClientSummaryDto>
        {
            new(clientId, "CL-DEL", "عميل للحذف", null, null)
        };

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<ClientList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().Be(clientId);
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Once);
    }

    private sealed class FakeClientListHttpMessageHandler : HttpMessageHandler
    {
        public List<ClientSummaryDto> Clients { get; set; } = new();
        public List<ClientSummaryDto> SearchResults { get; set; } = new();
        public string? LastSearchTerm { get; private set; }
        public Guid? LastDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/api/clients/search?term="))
            {
                var queryIndex = path.IndexOf("term=", StringComparison.Ordinal);
                LastSearchTerm = queryIndex >= 0 ? Uri.UnescapeDataString(path.Substring(queryIndex + 5)) : null;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, SearchResults));
            }

            if (request.Method == HttpMethod.Get && (path.EndsWith("/api/clients") || path.EndsWith("/api/clients/")))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Clients));
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/api/clients/"))
            {
                var parts = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && Guid.TryParse(parts[^1], out var id))
                {
                    LastDeletedId = id;
                    Clients = Clients.Where(c => c.Id != id).ToList();
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
