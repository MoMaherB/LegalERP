using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Companies;
using LegalERP.Web.Components.Pages.Companies.Sections;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyDetailOrchestratorTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly Mock<AuthenticationStateProvider> _mockAuth = new();
    private readonly FakeHttpMessageHandler _handler = new();

    public CompanyDetailOrchestratorTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("TestEditor");
        auth.SetRoles("Editor");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new FinancialsApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void CompanyDetail_WhenLoaded_OrchestratesAllThreeChildSections()
    {
        // Arrange
        var companyId = Guid.NewGuid();
        var company = new CompanyDto(
            Id: companyId,
            FileNumber: "F-100",
            Category: CompanyCategory.CapitalCompany,
            CompanyName: "Acme Legal Corp",
            CompanyNameEn: "Acme Legal Corp EN",
            TradeName: "Acme",
            EstablishmentDate: new DateOnly(2025, 1, 1),
            RegistrationNumber: "CR-9999",
            Address: "Giza",
            IncorporationDocumentId: null,
            IncorporationDocument: null,
            HasManyPartners: false,
            PartnersText: null,
            PartnersIdsDocumentId: null,
            PartnersIdsDocument: null,
            PartnersAttorneysDocumentId: null,
            PartnersAttorneysDocument: null,
            Amendments: new List<CompanyAmendmentDto>(),
            Partners: new List<CompanyPartnerDto>()
        );

        _handler.SetCompany(company);

        // Act: Render the host orchestrator page
        var cut = RenderComponent<CompanyDetail>(parameters => parameters
            .Add(p => p.Id, companyId)
        );

        // Assert: Verify host renders company name header
        cut.Find("h1.ios-title").TextContent.Should().Contain("Acme Legal Corp");

        // Assert: Verify all 3 child sections are mounted
        cut.FindComponents<CompanyPartnersSection>().Should().HaveCount(1);
        cut.FindComponents<CompanyAmendmentsSection>().Should().HaveCount(1);
        cut.FindComponents<EntityDocumentsSection>().Should().HaveCount(1);
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private string _companyJson = "{}";

        public void SetCompany(CompanyDto company)
        {
            _companyJson = JsonSerializer.Serialize(company);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            string content;
            if (uri.Contains("api/companies/"))
            {
                content = _companyJson;
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
