using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using LegalERP.Application.Attorneys;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Attorneys;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Attorneys;

public class AttorneyFormTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly AttorneyFormRecordingHandler _handler = new();

    public AttorneyFormTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new AttorneyApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new DocumentApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void AttorneyForm_CreateMode_RendersInitialFormWithEmptyFieldsAndAddTitle()
    {
        var cut = RenderComponent<AttorneyForm>();

        cut.Find("h1.ios-title").TextContent.Should().Be("AddAttorney");
        cut.Find("input[placeholder='AttorneyNumberPlaceholder']").GetAttribute("value").Should().BeNullOrEmpty();
        cut.Find("textarea[placeholder='NotesPlaceholder']").TextContent.Should().BeNullOrEmpty();
        cut.Find("button[type='submit']").TextContent.Should().Contain("CreateAttorney");
    }

    [Fact]
    public async Task AttorneyForm_CreateMode_WhenAttorneyNumberIsEmpty_ShowsWarningAndDoesNotCallApi()
    {
        var cut = RenderComponent<AttorneyForm>();

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _mockToast.Verify(t => t.ShowWarning("AttorneyNumberRequired"), Times.Once);
        _handler.LastCreatedAttorney.Should().BeNull();
    }

    [Fact]
    public async Task AttorneyForm_CreateMode_WithFile_CreatesAttorneyFirstThenUploadsDocumentWithCreatedIdAndUpdatesAttorney()
    {
        var cut = RenderComponent<AttorneyForm>();

        cut.Find("input[placeholder='AttorneyNumberPlaceholder']").Change("ATT-CREATE-001");
        cut.Find("textarea[placeholder='NotesPlaceholder']").Change("ملاحظات إنشاء توكيل");

        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("POA scan bytes", "poa-scan.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        // 1. Created attorney first without document
        _handler.LastCreatedAttorney.Should().NotBeNull();
        _handler.LastCreatedAttorney!.AttorneyNumber.Should().Be("ATT-CREATE-001");
        _handler.LastCreatedAttorney.Notes.Should().Be("ملاحظات إنشاء توكيل");
        _handler.LastCreatedAttorney.DocumentId.Should().BeNull();

        // 2. Document uploaded using created attorney ID and DocumentOwnerTypes.Attorney
        _handler.LastUploadedOwnerType.Should().Be(DocumentOwnerTypes.Attorney);
        _handler.LastUploadedOwnerId.Should().Be(_handler.CreatedAttorneyId);

        // 3. Attorney updated with uploaded document ID
        _handler.LastUpdatedAttorneyId.Should().Be(_handler.CreatedAttorneyId);
        _handler.LastUpdatedAttorney.Should().NotBeNull();
        _handler.LastUpdatedAttorney!.DocumentId.Should().Be(_handler.UploadedDocumentId);
        _handler.LastUpdatedAttorney.AttorneyNumber.Should().Be("ATT-CREATE-001");

        // 4. Success toast shown
        _mockToast.Verify(t => t.ShowSuccess("AttorneyCreated"), Times.Once);
    }

    [Fact]
    public async Task AttorneyForm_CreateMode_WithoutFile_CreatesAttorneyAndNavigates()
    {
        var cut = RenderComponent<AttorneyForm>();

        cut.Find("input[placeholder='AttorneyNumberPlaceholder']").Change("ATT-NO-DOC");
        cut.Find("textarea[placeholder='NotesPlaceholder']").Change("توكيل بدون مستند");

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.LastCreatedAttorney.Should().NotBeNull();
        _handler.LastCreatedAttorney!.AttorneyNumber.Should().Be("ATT-NO-DOC");
        _handler.LastUploadedOwnerId.Should().BeNull();
        _handler.LastUpdatedAttorneyId.Should().BeNull();
        _mockToast.Verify(t => t.ShowSuccess("AttorneyCreated"), Times.Once);
    }

    [Fact]
    public void AttorneyForm_EditMode_LoadsExistingAttorney_DisplaysValuesAndCurrentDocumentName()
    {
        var attorneyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        _handler.ExistingAttorney = new AttorneyDto(
            Id: attorneyId,
            AttorneyNumber: "ATT-EXISTING-99",
            Notes: "ملاحظات توكيل قديم",
            Document: new DocumentDto(docId, "existing-poa.pdf", "stored-poa.pdf", "application/pdf", 4096),
            Clients: []
        );

        var cut = RenderComponent<AttorneyForm>(p => p.Add(x => x.Id, attorneyId));

        cut.Find("h1.ios-title").TextContent.Should().Be("EditAttorney");
        cut.Find("input[placeholder='AttorneyNumberPlaceholder']").GetAttribute("value").Should().Be("ATT-EXISTING-99");
        cut.Find("textarea[placeholder='NotesPlaceholder']").GetAttribute("value").Should().Be("ملاحظات توكيل قديم");
        cut.Find("small.text-success").TextContent.Should().Contain("existing-poa.pdf");
        cut.Find("button[type='submit']").TextContent.Should().Contain("SaveChanges");
    }

    [Fact]
    public async Task AttorneyForm_EditMode_WithNewFile_ReplacesDocumentInPlaceAndUpdatesAttorney()
    {
        var attorneyId = Guid.NewGuid();
        var existingDocId = Guid.NewGuid();
        _handler.ExistingAttorney = new AttorneyDto(
            Id: attorneyId,
            AttorneyNumber: "ATT-TO-UPDATE",
            Notes: "Old notes",
            Document: new DocumentDto(existingDocId, "old-poa.pdf", "stored-old.pdf", "application/pdf", 1024),
            Clients: []
        );

        var cut = RenderComponent<AttorneyForm>(p => p.Add(x => x.Id, attorneyId));

        cut.Find("input[placeholder='AttorneyNumberPlaceholder']").Change("ATT-UPDATED-NAME");
        cut.Find("textarea[placeholder='NotesPlaceholder']").Change("Updated notes");

        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("Replacement bytes", "new-poa.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        // Document replaced via ReplaceDocumentAsync on existingDocId
        _handler.ReplacedDocumentIds.Should().ContainSingle().Which.Should().Be(existingDocId);
        _handler.LastUpdatedAttorneyId.Should().Be(attorneyId);
        _handler.LastUpdatedAttorney.Should().NotBeNull();
        _handler.LastUpdatedAttorney!.AttorneyNumber.Should().Be("ATT-UPDATED-NAME");
        _handler.LastUpdatedAttorney.Notes.Should().Be("Updated notes");
        _handler.LastUpdatedAttorney.DocumentId.Should().Be(existingDocId);
        _mockToast.Verify(t => t.ShowSuccess("AttorneyUpdated"), Times.Once);
    }

    [Fact]
    public async Task AttorneyForm_EditMode_WithoutNewFile_PreservesExistingDocumentIdAndUpdatesAttorney()
    {
        var attorneyId = Guid.NewGuid();
        var existingDocId = Guid.NewGuid();
        _handler.ExistingAttorney = new AttorneyDto(
            Id: attorneyId,
            AttorneyNumber: "ATT-SAME-DOC",
            Notes: "Old notes",
            Document: new DocumentDto(existingDocId, "keep-poa.pdf", "stored-keep.pdf", "application/pdf", 2048),
            Clients: []
        );

        var cut = RenderComponent<AttorneyForm>(p => p.Add(x => x.Id, attorneyId));

        cut.Find("textarea[placeholder='NotesPlaceholder']").Change("Changed notes only");

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.ReplacedDocumentIds.Should().BeEmpty();
        _handler.LastUploadedOwnerId.Should().BeNull();
        _handler.LastUpdatedAttorneyId.Should().Be(attorneyId);
        _handler.LastUpdatedAttorney.Should().NotBeNull();
        _handler.LastUpdatedAttorney!.AttorneyNumber.Should().Be("ATT-SAME-DOC");
        _handler.LastUpdatedAttorney.Notes.Should().Be("Changed notes only");
        _handler.LastUpdatedAttorney.DocumentId.Should().Be(existingDocId);
        _mockToast.Verify(t => t.ShowSuccess("AttorneyUpdated"), Times.Once);
    }

    [Fact]
    public void AttorneyForm_EditMode_WhenAttorneyNotFound_ShowsErrorToast()
    {
        var attorneyId = Guid.NewGuid();
        _handler.ExistingAttorney = null;

        var cut = RenderComponent<AttorneyForm>(p => p.Add(x => x.Id, attorneyId));

        _mockToast.Verify(t => t.ShowError("AttorneyNotFound"), Times.Once);
    }

    private class AttorneyFormRecordingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public Guid CreatedAttorneyId { get; } = Guid.NewGuid();
        public Guid UploadedDocumentId { get; } = Guid.NewGuid();
        public AttorneyDto? ExistingAttorney { get; set; }

        public CreateAttorneyDto? LastCreatedAttorney { get; private set; }
        public Guid? LastUpdatedAttorneyId { get; private set; }
        public UpdateAttorneyDto? LastUpdatedAttorney { get; private set; }
        public string? LastUploadedOwnerType { get; private set; }
        public Guid? LastUploadedOwnerId { get; private set; }
        public List<Guid> ReplacedDocumentIds { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var query = request.RequestUri?.Query ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/attorneys/", StringComparison.OrdinalIgnoreCase))
            {
                if (ExistingAttorney == null)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ExistingAttorney, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.Equals("/api/attorneys", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastCreatedAttorney = JsonSerializer.Deserialize<CreateAttorneyDto>(body, JsonOptions);

                var summary = new AttorneySummaryDto(
                    CreatedAttorneyId,
                    LastCreatedAttorney?.AttorneyNumber ?? string.Empty,
                    0
                );

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(summary, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Put && path.StartsWith("/api/attorneys/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var id))
                    LastUpdatedAttorneyId = id;

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastUpdatedAttorney = JsonSerializer.Deserialize<UpdateAttorneyDto>(body, JsonOptions);

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/documents/upload", StringComparison.OrdinalIgnoreCase))
            {
                var queryParams = query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.OrdinalIgnoreCase);

                if (queryParams.TryGetValue("ownerType", out var ot))
                    LastUploadedOwnerType = ot;
                if (queryParams.TryGetValue("ownerId", out var oid) && Guid.TryParse(oid, out var parsedGuid))
                    LastUploadedOwnerId = parsedGuid;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"\"{UploadedDocumentId}\"", Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Put && path.EndsWith("/replace", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3 && Guid.TryParse(segments[^2], out var docId))
                    ReplacedDocumentIds.Add(docId);

                var dto = new DocumentDto(UploadedDocumentId, "new-poa.pdf", "stored-new.pdf", "application/pdf", 2048);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(dto, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
