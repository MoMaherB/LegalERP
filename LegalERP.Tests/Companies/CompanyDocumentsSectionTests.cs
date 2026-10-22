using Bunit;
using FluentAssertions;
using LegalERP.Application.Companies;
using LegalERP.Web;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyDocumentsSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeHttpMessageHandler _handler = new();

    public CompanyDocumentsSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
    }

    private IRenderedComponent<EntityDocumentsSection> RenderCompanyDocuments(Guid companyId, bool canEdit, bool canDelete) =>
        RenderComponent<EntityDocumentsSection>(parameters => parameters
            .Add(p => p.OwnerType, "companydoc")
            .Add(p => p.OwnerId, companyId)
            .Add(p => p.TitleKey, "CompanyDocuments")
            .Add(p => p.EmptyMessageKey, "NoCompanyDocuments")
            .Add(p => p.CanEdit, canEdit)
            .Add(p => p.CanDelete, canDelete));

    [Fact]
    public void CompanyDocumentsSection_WhenNoDocumentsExist_RendersNoCompanyDocumentsAlert()
    {
        var companyId = Guid.NewGuid();
        _handler.SetResponse(new List<DocumentDto>());

        var cut = RenderCompanyDocuments(companyId, canEdit: false, canDelete: false);

        cut.Find(".alert").TextContent.Should().Contain("NoCompanyDocuments");
    }

    [Fact]
    public void CompanyDocumentsSection_WhenCanEditIsTrue_RendersUploadDocumentButton()
    {
        var companyId = Guid.NewGuid();
        _handler.SetResponse(new List<DocumentDto>());

        var cut = RenderCompanyDocuments(companyId, canEdit: true, canDelete: false);

        cut.Find("button.ios-btn-primary").TextContent.Should().Contain("UploadDocument");
    }

    [Fact]
    public void CompanyDocumentsSection_WhenCanEditIsFalse_DoesNotRenderUploadDocumentButton()
    {
        var companyId = Guid.NewGuid();
        _handler.SetResponse(new List<DocumentDto>());

        var cut = RenderCompanyDocuments(companyId, canEdit: false, canDelete: false);

        cut.FindAll("button.ios-btn-primary").Should().BeEmpty();
    }

    [Fact]
    public void CompanyDocumentsSection_WhenDocumentsExist_RendersDocumentCardsWithFormattedSize()
    {
        var companyId = Guid.NewGuid();
        var docs = new List<DocumentDto>
        {
            new(Guid.NewGuid(), "contract.pdf", "stored1.pdf", "application/pdf", 2048)
        };
        _handler.SetResponse(docs);

        var cut = RenderCompanyDocuments(companyId, canEdit: true, canDelete: true);

        cut.Find(".ios-details").TextContent.Should().Contain("2.0 KB");
        cut.FindAll(".ios-btn-danger").Should().HaveCount(1);
    }

    [Fact]
    public void CompanyDocumentsSection_WhenCanDeleteIsFalse_DoesNotRenderDeleteButtons()
    {
        var companyId = Guid.NewGuid();
        var docs = new List<DocumentDto>
        {
            new(Guid.NewGuid(), "contract.pdf", "stored1.pdf", "application/pdf", 2048)
        };
        _handler.SetResponse(docs);

        var cut = RenderCompanyDocuments(companyId, canEdit: true, canDelete: false);

        cut.FindAll(".ios-btn-danger").Should().BeEmpty();
    }

    [Fact]
    public void CompanyDocumentsSection_ClickingUploadButton_TogglesFormOpen()
    {
        var companyId = Guid.NewGuid();
        _handler.SetResponse(new List<DocumentDto>());

        var cut = RenderCompanyDocuments(companyId, canEdit: true, canDelete: false);

        var toggleButton = cut.Find("button.ios-btn-primary");
        toggleButton.Click();

        cut.Find("input.ios-input[placeholder='DocTitlePlaceholder']").Should().NotBeNull();
        toggleButton.TextContent.Should().Contain("Cancel");
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private string _json = "[]";

        public void SetResponse<T>(T data)
        {
            _json = JsonSerializer.Serialize(data);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
