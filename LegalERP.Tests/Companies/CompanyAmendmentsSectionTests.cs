using Bunit;
using FluentAssertions;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Companies.Sections;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyAmendmentsSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();

    public CompanyAmendmentsSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var handler = new FakeHttpMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
    }

    private static CompanyDto CreateCompany(List<CompanyAmendmentDto>? amendments = null)
    {
        return new CompanyDto(
            Id: Guid.NewGuid(),
            FileNumber: "F-100",
            Category: CompanyCategory.CapitalCompany,
            CompanyName: "Test Co",
            CompanyNameEn: "Test Co EN",
            TradeName: "Test Trade",
            EstablishmentDate: new DateOnly(2025, 1, 1),
            RegistrationNumber: "CR-12345",
            Address: "Cairo",
            IncorporationDocumentId: null,
            IncorporationDocument: null,
            HasManyPartners: false,
            PartnersText: null,
            PartnersIdsDocumentId: null,
            PartnersIdsDocument: null,
            PartnersAttorneysDocumentId: null,
            PartnersAttorneysDocument: null,
            Amendments: amendments ?? new List<CompanyAmendmentDto>(),
            Partners: new List<CompanyPartnerDto>()
        );
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenNoAmendmentsExist_RendersEmptyStateAlert()
    {
        // Arrange
        var company = CreateCompany(new List<CompanyAmendmentDto>());

        // Act
        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        // Assert
        cut.Find(".alert").TextContent.Should().Contain("NoAmendmentsRecorded");
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenCanEditIsTrue_RendersAddAmendmentButton()
    {
        // Arrange
        var company = CreateCompany();

        // Act
        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, false)
        );

        // Assert
        cut.Find("button.ios-btn-primary").TextContent.Should().Contain("AddAmendment");
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenCanEditIsFalse_DoesNotRenderAddAmendmentButton()
    {
        // Arrange
        var company = CreateCompany();

        // Act
        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        // Assert
        cut.FindAll("button.ios-btn-primary").Should().BeEmpty();
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenAmendmentsExist_RendersOrderedCardsWithSequenceBadges()
    {
        // Arrange: Provide out-of-order sequence amendments (seq 2, then seq 1)
        var amendments = new List<CompanyAmendmentDto>
        {
            new(Guid.NewGuid(), 2, "Second Amendment", new DateOnly(2025, 3, 1), null, null),
            new(Guid.NewGuid(), 1, "First Amendment", new DateOnly(2025, 2, 1), null, null)
        };
        var company = CreateCompany(amendments);

        // Act
        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
        );

        // Assert: Verify sorting order in rendered badges
        var badges = cut.FindAll(".ios-status-badge.ios-status-gray");
        badges.Should().HaveCount(2);
        badges[0].TextContent.Should().Be("#1");
        badges[1].TextContent.Should().Be("#2");

        // Verify Delete button rendered because CanDelete is true
        var deleteButtons = cut.FindAll(".ios-btn-danger");
        deleteButtons.Should().HaveCount(2);
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenCanDeleteIsFalse_DoesNotRenderDeleteButtons()
    {
        // Arrange
        var amendments = new List<CompanyAmendmentDto>
        {
            new(Guid.NewGuid(), 1, "First Amendment", new DateOnly(2025, 2, 1), null, null)
        };
        var company = CreateCompany(amendments);

        // Act
        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, false)
        );

        // Assert
        cut.FindAll(".ios-btn-danger").Should().BeEmpty();
    }

    [Fact]
    public void CompanyAmendmentsSection_ClickingAddButton_TogglesFormWithCalculatedPlaceholder()
    {
        // Arrange
        var amendments = new List<CompanyAmendmentDto>
        {
            new(Guid.NewGuid(), 1, "First Amendment", null, null, null)
        };
        var company = CreateCompany(amendments);

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, false)
        );

        // Act: Click "AddAmendment" button
        var toggleButton = cut.Find("button.ios-btn-primary");
        toggleButton.Click();

        // Assert: Form is visible with title input placeholder matching sequence 2 ("عقد التعديل الثاني")
        var titleInput = cut.Find("input.ios-input[placeholder='عقد التعديل الثاني']");
        titleInput.Should().NotBeNull();
        toggleButton.TextContent.Should().Contain("Cancel");
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
