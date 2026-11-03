using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Attorneys;
using LegalERP.Application.Clients;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Clients;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientDetailTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeClientDetailHttpMessageHandler _httpHandler = new();

    public ClientDetailTests()
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
        Services.AddSingleton(new AttorneyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new DocumentApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void ClientDetail_WhenLoaded_RendersPersonalInfoCard()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.Client = new ClientDto(
            clientId,
            "CL-101",
            "أحمد عبد العزيز",
            "Ahmed Abdelaziz",
            "29001011234567",
            "01012345678",
            "ahmed@example.com",
            "مدينة نصر، القاهرة",
            "ملاحظات خاصة بالقضية",
            null,
            new List<ClientCaseDto>(),
            new List<ClientCompanyDto>()
        );

        var cut = RenderComponent<ClientDetail>(parameters => parameters.Add(p => p.Id, clientId));

        cut.WaitForAssertion(() =>
        {
            cut.Find(".ios-title").TextContent.Should().Contain("أحمد عبد العزيز");
            cut.Markup.Should().Contain("Ahmed Abdelaziz");
            cut.Markup.Should().Contain("CL-101");
            cut.Markup.Should().Contain("29001011234567");
            cut.Markup.Should().Contain("01012345678");
            cut.Markup.Should().Contain("ahmed@example.com");
            cut.Markup.Should().Contain("مدينة نصر، القاهرة");
            cut.Markup.Should().Contain("ملاحظات خاصة بالقضية");
        });
    }

    [Fact]
    public void ClientDetail_RelatedCases_RendersLocalizedCaseTypesAndCriminalVsCivilRoles()
    {
        var clientId = Guid.NewGuid();
        var caseId1 = Guid.NewGuid();
        var caseId2 = Guid.NewGuid();

        _httpHandler.Client = new ClientDto(
            clientId,
            "CL-102",
            "محمد الشامي",
            null,
            "28505051234567",
            "01112223334",
            null,
            null,
            null,
            null,
            new List<ClientCaseDto>
            {
                new(
                    caseId1,
                    "101/2026",
                    "جنحة مباشرة",
                    CaseType.Criminal,
                    CaseStatus.Active,
                    null,
                    PartyRole.Defendant,
                    true
                ),
                new(
                    caseId2,
                    "202/2026",
                    "دعوى صحة ونفاذ",
                    CaseType.Civil,
                    CaseStatus.Closed,
                    CaseOutcome.Won,
                    PartyRole.Defendant,
                    false
                )
            },
            new List<ClientCompanyDto>()
        );

        var cut = RenderComponent<ClientDetail>(parameters => parameters.Add(p => p.Id, clientId));

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(2);

            // Row 1: Criminal, Defendant -> DefendantCriminal, CaseType -> Criminal
            var row1Text = rows[0].TextContent;
            row1Text.Should().Contain("101/2026");
            row1Text.Should().Contain("جنحة مباشرة");
            row1Text.Should().Contain("Criminal");
            row1Text.Should().Contain("DefendantCriminal");

            // Row 2: Civil, Defendant -> DefendantCivil, CaseType -> Civil
            var row2Text = rows[1].TextContent;
            row2Text.Should().Contain("202/2026");
            row2Text.Should().Contain("دعوى صحة ونفاذ");
            row2Text.Should().Contain("Civil");
            row2Text.Should().Contain("DefendantCivil");
            row2Text.Should().Contain("ClosedWon");
        });
    }

    [Fact]
    public void ClientDetail_RelatedCompanies_RendersCompanyListWithOwnershipPercentage()
    {
        var clientId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        _httpHandler.Client = new ClientDto(
            clientId,
            "CL-103",
            "فاطمة الزهراء",
            null,
            "29202021234567",
            null,
            null,
            null,
            null,
            null,
            new List<ClientCaseDto>(),
            new List<ClientCompanyDto>
            {
                new(companyId, "شركة الفرات الدولية", 25.5m)
            }
        );

        var cut = RenderComponent<ClientDetail>(parameters => parameters.Add(p => p.Id, clientId));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("شركة الفرات الدولية");
            cut.Markup.Should().Contain("25.5");
        });
    }

    [Fact]
    public void ClientDetail_Attorneys_RendersLinkedAttorneyCards()
    {
        var clientId = Guid.NewGuid();
        var attorneyId = Guid.NewGuid();

        _httpHandler.Client = new ClientDto(
            clientId,
            "CL-104",
            "عمر الفاروق",
            null,
            "29003031234567",
            null,
            null,
            null,
            null,
            null,
            new List<ClientCaseDto>(),
            new List<ClientCompanyDto>()
        );

        _httpHandler.Attorneys = new List<ClientAttorneyDto>
        {
            new(attorneyId, "ATT-999", null)
        };

        var cut = RenderComponent<ClientDetail>(parameters => parameters.Add(p => p.Id, clientId));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("ATT-999");
            cut.Markup.Should().Contain($"/attorneys/{attorneyId}");
        });
    }

    [Fact]
    public async Task ClientDetail_DeleteClient_WhenConfirmed_CallsDeleteApiAndNavigates()
    {
        var clientId = Guid.NewGuid();
        _httpHandler.Client = new ClientDto(
            clientId,
            "CL-105",
            "عميل سيتم حذفه",
            null,
            "29004041234567",
            null,
            null,
            null,
            null,
            null,
            new List<ClientCaseDto>(),
            new List<ClientCompanyDto>()
        );

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<ClientDetail>(parameters => parameters.Add(p => p.Id, clientId));

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().Be(clientId);
        nav.Uri.Should().EndWith("/clients");
    }

    private sealed class FakeClientDetailHttpMessageHandler : HttpMessageHandler
    {
        public ClientDto? Client { get; set; }
        public List<ClientAttorneyDto> Attorneys { get; set; } = new();
        public Guid? LastDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/api/clients/") && !path.Contains("/attorneys"))
            {
                if (Client is null) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Client));
            }

            if (request.Method == HttpMethod.Get && path.Contains("/api/attorneys/by-client/"))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Attorneys));
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/api/clients/"))
            {
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && Guid.TryParse(parts[^1], out var id))
                {
                    LastDeletedId = id;
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
