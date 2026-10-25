using Bunit;
using FluentAssertions;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Companies.Sections;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyAmendmentsSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeHttpMessageHandler _handler = new();

    public CompanyAmendmentsSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new DocumentApiClient(_mockHttpFactory.Object));
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
        var company = CreateCompany(new List<CompanyAmendmentDto>());

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        cut.Find(".alert").TextContent.Should().Contain("NoAmendmentsRecorded");
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenCanEditIsTrue_RendersAddAmendmentButton()
    {
        var company = CreateCompany();

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, false)
        );

        cut.Find("button.ios-btn-primary").TextContent.Should().Contain("AddAmendment");
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenCanEditIsFalse_DoesNotRenderAddAmendmentButton()
    {
        var company = CreateCompany();

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        cut.FindAll("button.ios-btn-primary").Should().BeEmpty();
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenAmendmentsExist_RendersOrderedCardsWithSequenceBadges()
    {
        var amendments = new List<CompanyAmendmentDto>
        {
            new(Guid.NewGuid(), 2, "Second Amendment", new DateOnly(2025, 3, 1), null, null),
            new(Guid.NewGuid(), 1, "First Amendment", new DateOnly(2025, 2, 1), null, null)
        };
        var company = CreateCompany(amendments);

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
        );

        var badges = cut.FindAll(".ios-status-badge.ios-status-gray");
        badges.Should().HaveCount(2);
        badges[0].TextContent.Should().Be("#1");
        badges[1].TextContent.Should().Be("#2");

        var deleteButtons = cut.FindAll(".ios-btn-danger");
        deleteButtons.Should().HaveCount(2);
    }

    [Fact]
    public void CompanyAmendmentsSection_WhenCanDeleteIsFalse_DoesNotRenderDeleteButtons()
    {
        var amendments = new List<CompanyAmendmentDto>
        {
            new(Guid.NewGuid(), 1, "First Amendment", new DateOnly(2025, 2, 1), null, null)
        };
        var company = CreateCompany(amendments);

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, false)
        );

        cut.FindAll(".ios-btn-danger").Should().BeEmpty();
    }

    [Fact]
    public void CompanyAmendmentsSection_ClickingAddButton_TogglesFormWithCalculatedPlaceholder()
    {
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

        var toggleButton = cut.Find("button.ios-btn-primary");
        toggleButton.Click();

        var titleInput = cut.Find("input.ios-input[placeholder='عقد التعديل الثاني']");
        titleInput.Should().NotBeNull();
        toggleButton.TextContent.Should().Contain("Cancel");
    }

    [Fact]
    public async Task CompanyAmendmentsSection_WhenReplacingAmendmentDocument_ReplacesInPlaceViaDocumentApiWithoutRePuttingAmendment()
    {
        var amendmentId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var docDto = new DocumentDto(docId, "amendment1.pdf", "stored-amend1.pdf", "application/pdf", 2048);
        var amendments = new List<CompanyAmendmentDto>
        {
            new(amendmentId, 1, "First Amendment", new DateOnly(2025, 2, 1), docId, docDto)
        };
        var company = CreateCompany(amendments);
        var onChangedCalled = false;

        var cut = RenderComponent<CompanyAmendmentsSection>(parameters => parameters
            .Add(p => p.Company, company)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
            .Add(p => p.OnChanged, () => { onChangedCalled = true; })
        );

        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("new-amendment1.pdf");
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("updated amendment pdf")));

        var thumbnail = cut.FindComponent<FileThumbnail>();
        await cut.InvokeAsync(() => thumbnail.Instance.OnReplace.InvokeAsync(mockFile.Object));

        _handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Put &&
            r.Uri.Contains($"api/documents/{docId}/replace"));
        _handler.Requests.Should().NotContain(r => r.Uri.Contains("/amendments/"));
        _handler.Requests.Should().NotContain(r => r.Method == HttpMethod.Delete);
        onChangedCalled.Should().BeTrue();
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            Requests.Add((request.Method, uri));

            if (request.Method == HttpMethod.Put && uri.Contains("/replace"))
            {
                var dto = new DocumentDto(Guid.NewGuid(), "new-amendment1.pdf", "stored-new.pdf", "application/pdf", 2048);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
