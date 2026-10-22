using Bunit;
using FluentAssertions;
using LegalERP.Application.Cases;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Cases.Sections;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace LegalERP.Tests.Cases;

public class CaseMemosSectionTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly MemoRecordingHttpMessageHandler _httpHandler = new();

    public CaseMemosSectionTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CaseApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
    }

    private static CaseDto CreateCase(List<CaseMemoDto>? memos = null)
    {
        return new CaseDto(
            Id: Guid.NewGuid(),
            CaseNumber: "2026/700",
            Title: "قضية مدنية",
            CaseType: CaseType.Civil,
            Status: CaseStatus.Active,
            Outcome: null,
            FilingDate: new DateOnly(2026, 2, 1),
            CourtName: "محكمة القاهرة",
            JudgeName: null,
            Notes: null,
            Parties: new List<CasePartyDto>(),
            Memos: memos ?? new List<CaseMemoDto>(),
            Hearings: new List<CaseHearingDto>()
        );
    }

    [Fact]
    public void CaseMemosSection_WhenNoMemosExist_RendersEmptyStateAlert()
    {
        var caseDto = CreateCase(new List<CaseMemoDto>());

        var cut = RenderComponent<CaseMemosSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        cut.Find(".alert").TextContent.Should().Contain("NoLegalMemos");
    }

    [Fact]
    public void CaseMemosSection_WhenCanEditAndCanDeleteAreFalse_HidesAddUpdateAndDeleteControls()
    {
        var docId = Guid.NewGuid();
        var doc = new DocumentDto(docId, "memo.pdf", "stored-memo.pdf", "application/pdf", 4096);
        var memos = new List<CaseMemoDto>
        {
            new(Guid.NewGuid(), "مذكرة دفاع", "محتوى المذكرة", new DateOnly(2026, 3, 10), docId, doc)
        };
        var caseDto = CreateCase(memos);

        var cut = RenderComponent<CaseMemosSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, false)
            .Add(p => p.CanDelete, false)
        );

        cut.Markup.Should().Contain("مذكرة دفاع");
        cut.Markup.Should().Contain("محتوى المذكرة");
        cut.FindAll("button.ios-btn-primary").Should().BeEmpty();
        cut.FindAll("button.ios-btn-danger").Should().BeEmpty();
        cut.Markup.Should().NotContain("UpdateFile");
    }

    [Fact]
    public async Task CaseMemosSection_WhenCreatingMemoWithFile_CreatesMemoFirstThenUploadsWithCreatedMemoId()
    {
        var caseDto = CreateCase();
        var onChangedCalled = false;

        var cut = RenderComponent<CaseMemosSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
            .Add(p => p.OnChanged, () => { onChangedCalled = true; })
        );

        // Open Add Memo form
        cut.Find("button.ios-btn-primary").Click();

        // Enter memo title and content
        cut.Find("input[placeholder='MemoTitlePlaceholder']").Change("مذكرة بالطلبات الختامية");
        cut.Find("textarea[placeholder='MemoContentPlaceholder']").Change("نص المذكرة القانونية");

        // Select a file via InputFile
        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("PDF content", "brief.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        // Click SaveLegalMemo
        var saveBtn = cut.FindAll("button.ios-btn-primary").First(b => b.TextContent.Contains("SaveLegalMemo"));
        await cut.InvokeAsync(() => saveBtn.Click());

        // Verify:
        // 1. Memo was created first with DocumentId == null
        _httpHandler.LastCreatedMemo.Should().NotBeNull();
        _httpHandler.LastCreatedMemo!.Title.Should().Be("مذكرة بالطلبات الختامية");
        _httpHandler.LastCreatedMemo.DocumentId.Should().BeNull();

        // 2. Document was uploaded with ownerType = "casememo" and ownerId == CreatedMemoId (never a random Guid!)
        _httpHandler.LastUploadedOwnerType.Should().Be("casememo");
        _httpHandler.LastUploadedOwnerId.Should().Be(_httpHandler.CreatedMemoId);

        // 3. Memo was updated with the uploaded DocumentId
        _httpHandler.LastUpdatedMemo.Should().NotBeNull();
        _httpHandler.LastUpdatedMemoId.Should().Be(_httpHandler.CreatedMemoId);
        _httpHandler.LastUpdatedMemo!.DocumentId.Should().Be(_httpHandler.UploadedDocumentId);
        onChangedCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CaseMemosSection_WhenReplacingMemoFile_UploadsWithMemoIdUpdatesMemoAndDeletesOldDocument()
    {
        var memoId = Guid.NewGuid();
        var oldDocId = Guid.NewGuid();
        var oldDoc = new DocumentDto(oldDocId, "old-brief.pdf", "old-brief.pdf", "application/pdf", 2048);
        var memos = new List<CaseMemoDto>
        {
            new(memoId, "مذكرة استئناف", "أسباب الاستئناف", new DateOnly(2026, 4, 1), oldDocId, oldDoc)
        };
        var caseDto = CreateCase(memos);
        var onChangedCalled = false;

        var cut = RenderComponent<CaseMemosSection>(parameters => parameters
            .Add(p => p.Case, caseDto)
            .Add(p => p.CanEdit, true)
            .Add(p => p.CanDelete, true)
            .Add(p => p.OnChanged, () => { onChangedCalled = true; })
        );

        // Find the inline replacement InputFile inside the memo card
        var inputFiles = cut.FindComponents<InputFile>();
        inputFiles.Should().ContainSingle();

        var replacementFile = InputFileContent.CreateFromText("Updated PDF", "new-brief.pdf", null, "application/pdf");
        await cut.InvokeAsync(() => inputFiles[0].UploadFiles(replacementFile));

        // Verify upload used ownerType="casememo" and ownerId=memoId
        _httpHandler.LastUploadedOwnerType.Should().Be("casememo");
        _httpHandler.LastUploadedOwnerId.Should().Be(memoId);

        // Verify memo was updated with new DocumentId
        _httpHandler.LastUpdatedMemoId.Should().Be(memoId);
        _httpHandler.LastUpdatedMemo.Should().NotBeNull();
        _httpHandler.LastUpdatedMemo!.DocumentId.Should().Be(_httpHandler.UploadedDocumentId);

        // Verify old document was deleted to prevent orphans
        _httpHandler.DeletedDocumentIds.Should().Contain(oldDocId);
        onChangedCalled.Should().BeTrue();
    }

    private class MemoRecordingHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public Guid CreatedMemoId { get; } = Guid.NewGuid();
        public Guid UploadedDocumentId { get; } = Guid.NewGuid();

        public CreateCaseMemoDto? LastCreatedMemo { get; private set; }
        public Guid? LastUpdatedMemoId { get; private set; }
        public UpdateCaseMemoDto? LastUpdatedMemo { get; private set; }
        public string? LastUploadedOwnerType { get; private set; }
        public Guid? LastUploadedOwnerId { get; private set; }
        public List<Guid> DeletedDocumentIds { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var query = request.RequestUri?.Query ?? string.Empty;

            if (request.Method == HttpMethod.Post && path.EndsWith("/memos", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastCreatedMemo = JsonSerializer.Deserialize<CreateCaseMemoDto>(body, JsonOptions);

                var responseDto = new CaseMemoDto(
                    CreatedMemoId,
                    LastCreatedMemo?.Title ?? string.Empty,
                    LastCreatedMemo?.Content,
                    LastCreatedMemo?.MemoDate ?? new DateOnly(2026, 1, 1),
                    LastCreatedMemo?.DocumentId,
                    null
                );

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseDto, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/api/documents/upload", StringComparison.OrdinalIgnoreCase))
            {
                // Parse ownerType and ownerId from query string (?ownerType=casememo&ownerId=...)
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

            if (request.Method == HttpMethod.Put && path.Contains("/memos/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var memoId))
                    LastUpdatedMemoId = memoId;

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastUpdatedMemo = JsonSerializer.Deserialize<UpdateCaseMemoDto>(body, JsonOptions);

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (request.Method == HttpMethod.Delete && path.StartsWith("/api/documents/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var docId))
                    DeletedDocumentIds.Add(docId);

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }
}
