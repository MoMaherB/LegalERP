using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Companies.Sections;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyPartnersSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakePartnersHttpMessageHandler _handler = new();

    public CompanyPartnersSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new DocumentApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
    }

    private static CompanyDto CreateCompany(
        bool hasManyPartners = false,
        List<CompanyPartnerDto>? partners = null)
    {
        return new CompanyDto(
            Id: Guid.NewGuid(),
            FileNumber: "F-200",
            Category: CompanyCategory.Partnership,
            CompanyName: "شركة النور",
            CompanyNameEn: "Al Nour Co",
            TradeName: "النور",
            EstablishmentDate: new DateOnly(2025, 1, 1),
            RegistrationNumber: "CR-200",
            Address: "Cairo",
            IncorporationDocumentId: null,
            IncorporationDocument: null,
            HasManyPartners: hasManyPartners,
            PartnersText: null,
            PartnersIdsDocumentId: null,
            PartnersIdsDocument: null,
            PartnersAttorneysDocumentId: null,
            PartnersAttorneysDocument: null,
            Amendments: new List<CompanyAmendmentDto>(),
            Partners: partners ?? new List<CompanyPartnerDto>()
        );
    }

    [Fact]
    public void CompanyPartnersSection_WhenStandardModeAndFormOpened_RendersClientQuickSearchSelector()
    {
        var company = CreateCompany(hasManyPartners: false);

        var cut = RenderComponent<CompanyPartnersSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true));

        cut.Find("button.ios-btn-primary").Click();

        cut.FindComponents<ClientQuickSearchSelector>().Should().ContainSingle();
        cut.Markup.Should().NotContain("ManyPartnersMode");
    }

    [Fact]
    public async Task CompanyPartnersSection_WhenManyPartnersMode_RendersDirectNameInputBulkSlotsAndSubmitsPartnerWithoutClientId()
    {
        var company = CreateCompany(hasManyPartners: true);
        var changedCalled = false;

        var cut = RenderComponent<CompanyPartnersSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
            .Add(p => p.OnChanged, () => { changedCalled = true; }));

        cut.Markup.Should().Contain("ManyPartnersMode");
        cut.Markup.Should().Contain("PartnersIds");
        cut.Markup.Should().Contain("PartnersAttorneys");

        cut.Find("button.ios-btn-primary").Click();
        cut.FindComponents<ClientQuickSearchSelector>().Should().BeEmpty();

        cut.Find("input[placeholder='PartnerName']").Change("أحمد الشريك");
        cut.Find("input[type='number']").Change("30.5");

        var saveBtn = cut.FindAll("button.ios-btn-primary")[1];
        await cut.InvokeAsync(() => saveBtn.Click());

        _handler.LastAddedPartner.Should().NotBeNull();
        _handler.LastAddedPartner!.ClientId.Should().BeNull();
        _handler.LastAddedPartner.FullName.Should().Be("أحمد الشريك");
        _handler.LastAddedPartner.OwnershipPercentage.Should().Be(30.5m);
        changedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CompanyPartnersSection_TogglePartnerActive_MovesPartnerBetweenActiveAndInactiveSectionsInPlace()
    {
        var partnerId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var partners = new List<CompanyPartnerDto>
        {
            new(partnerId, clientId, "محمد الشريك", "29001011234567", 50m, null, null, IsActive: true)
        };
        var company = CreateCompany(hasManyPartners: false, partners: partners);

        var cut = RenderComponent<CompanyPartnersSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true));

        cut.Markup.Should().Contain("ActivePartners (1)");
        cut.Markup.Should().NotContain("InactivePartners");
        cut.Find($"a[href='/clients/{clientId}']").TextContent.Should().Contain("محمد الشريك");

        var toggleBtn = cut.Find("button.ios-btn-warning");
        await cut.InvokeAsync(() => toggleBtn.Click());

        _handler.ToggleActiveCallCount.Should().Be(1);
        cut.Markup.Should().Contain("ActivePartners (0)");
        cut.Markup.Should().Contain("InactivePartners (1)");
    }

    private sealed class FakePartnersHttpMessageHandler : HttpMessageHandler
    {
        public CreateCompanyPartnerDto? LastAddedPartner { get; private set; }
        public int ToggleActiveCallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Post && path.EndsWith("/partners", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastAddedPartner = JsonSerializer.Deserialize<CreateCompanyPartnerDto>(
                    body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                var created = new CompanyPartnerDto(
                    Guid.NewGuid(),
                    LastAddedPartner?.ClientId,
                    LastAddedPartner?.FullName ?? "Partner",
                    null,
                    LastAddedPartner?.OwnershipPercentage,
                    null,
                    null,
                    true);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(created), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Put && path.EndsWith("/toggle-active", StringComparison.OrdinalIgnoreCase))
            {
                ToggleActiveCallCount++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        }
    }
}
