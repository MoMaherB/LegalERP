using Bunit;
using FluentAssertions;
using LegalERP.Application.Companies;
using LegalERP.Web;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace LegalERP.Tests.Cases;

public class EntityDocumentsSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly RecordingHttpMessageHandler _handler = new();

    public EntityDocumentsSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void EntityDocumentsSection_ForCaseOwner_LoadsCaseDocumentsAndRendersEmptyMessage()
    {
        var caseId = Guid.NewGuid();
        _handler.SetDocuments(new List<DocumentDto>());

        var cut = RenderComponent<EntityDocumentsSection>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId)
            .Add(x => x.TitleKey, "CaseDocuments")
            .Add(x => x.EmptyMessageKey, "NoCaseDocuments")
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, false));

        cut.Find("h3.ios-title").TextContent.Should().Contain("CaseDocuments");
        cut.Find(".alert").TextContent.Should().Contain("NoCaseDocuments");
        _handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Get &&
            r.Uri.Contains($"api/documents?ownerType=case&ownerId={caseId}"));
    }

    [Fact]
    public void EntityDocumentsSection_WhenDocumentsExist_FormatsFileSizeAndRespectsCanDelete()
    {
        var caseId = Guid.NewGuid();
        _handler.SetDocuments(new List<DocumentDto>
        {
            new(Guid.NewGuid(), "evidence.pdf", "stored.pdf", "application/pdf", 3072)
        });

        var cut = RenderComponent<EntityDocumentsSection>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId)
            .Add(x => x.TitleKey, "CaseDocuments")
            .Add(x => x.EmptyMessageKey, "NoCaseDocuments")
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true));

        cut.Find("small.ios-details").TextContent.Should().Contain("3.0 KB");
        cut.FindAll("button.ios-btn-danger").Should().HaveCount(1);
    }

    [Fact]
    public void EntityDocumentsSection_UploadingDocument_UploadsWithOwnerTypeAndReloads()
    {
        var caseId = Guid.NewGuid();
        _handler.SetDocuments(new List<DocumentDto>());

        var cut = RenderComponent<EntityDocumentsSection>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId)
            .Add(x => x.TitleKey, "CaseDocuments")
            .Add(x => x.EmptyMessageKey, "NoCaseDocuments")
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true));

        cut.Find("button.ios-btn-primary").Click();

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromText("Case attachment", "evidence.pdf", contentType: "application/pdf"));

        // Selecting a file defaults the custom title if left blank
        cut.Find("input[placeholder='DocTitlePlaceholder']").GetAttribute("value").Should().Be("evidence.pdf");

        var uploadBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("UploadSaveDoc"));
        uploadBtn.Click();

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Post &&
            r.Uri.Contains($"ownerType=case&ownerId={caseId}"));
    }

    [Fact]
    public async Task EntityDocumentsSection_ReplacingDocumentInline_UploadsNewAndDeletesOldDocument()
    {
        var caseId = Guid.NewGuid();
        var oldDocId = Guid.NewGuid();
        _handler.SetDocuments(new List<DocumentDto>
        {
            new(oldDocId, "old.pdf", "stored-old.pdf", "application/pdf", 1024)
        });

        var cut = RenderComponent<EntityDocumentsSection>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId)
            .Add(x => x.TitleKey, "CaseDocuments")
            .Add(x => x.EmptyMessageKey, "NoCaseDocuments")
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true));

        var thumbnail = cut.FindComponent<FileThumbnail>();
        var mockFile = new Mock<IBrowserFile>();
        mockFile.Setup(f => f.Name).Returns("replacement.pdf");
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");
        mockFile.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes("new-content")));

        await cut.InvokeAsync(() => thumbnail.Instance.OnReplace.InvokeAsync(mockFile.Object));

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Post &&
            r.Uri.Contains($"ownerType=case&ownerId={caseId}"));
        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete &&
            r.Uri.EndsWith($"api/documents/{oldDocId}"));
    }

    [Fact]
    public void EntityDocumentsSection_DeletingDocument_ConfirmsAndDeletesDocument()
    {
        var caseId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        _handler.SetDocuments(new List<DocumentDto>
        {
            new(docId, "evidence.pdf", "stored.pdf", "application/pdf", 1024)
        });
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<EntityDocumentsSection>(p => p
            .Add(x => x.OwnerType, "case")
            .Add(x => x.OwnerId, caseId)
            .Add(x => x.TitleKey, "CaseDocuments")
            .Add(x => x.EmptyMessageKey, "NoCaseDocuments")
            .Add(x => x.CanEdit, true)
            .Add(x => x.CanDelete, true));

        cut.Find("button.ios-btn-danger").Click();

        _handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete &&
            r.Uri.EndsWith($"api/documents/{docId}"));
    }

    private class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private List<DocumentDto> _docs = new();
        public List<(HttpMethod Method, string Uri)> Requests { get; } = new();

        public void SetDocuments(List<DocumentDto> docs) => _docs = docs;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";
            Requests.Add((request.Method, uri));

            if (request.Method == HttpMethod.Post && uri.Contains("api/documents/upload"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"\"{Guid.NewGuid()}\"", System.Text.Encoding.UTF8, "application/json")
                });
            }

            if (request.Method == HttpMethod.Delete)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(_docs), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
