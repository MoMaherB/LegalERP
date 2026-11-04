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
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Companies;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyFormTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly CompanyFormRecordingHandler _handler = new();

    public CompanyFormTests()
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

    [Fact]
    public void CompanyForm_CreateMode_RendersInitialFormWithDefaults()
    {
        var cut = RenderComponent<CompanyForm>();

        cut.Find("h1.ios-title").TextContent.Should().Be("AddCompany");
        cut.Find("button[type='submit']").TextContent.Should().Contain("SaveCompany");
    }

    [Fact]
    public async Task CompanyForm_CreateMode_WhenNameEmpty_ShowsWarningToast()
    {
        var cut = RenderComponent<CompanyForm>();

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _mockToast.Verify(t => t.ShowWarning("CompanyNameRequired"), Times.Once);
        _handler.LastCreatedCompany.Should().BeNull();
    }

    [Fact]
    public async Task CompanyForm_CreateMode_WithIncorporationFile_CreatesCompanyFirstThenUploadsDocumentWithCreatedIdAndUpdatesCompany()
    {
        var cut = RenderComponent<CompanyForm>();

        // Set name
        var inputs = cut.FindAll("input.ios-input");
        var nameInput = inputs.First(i => i.ParentElement?.TextContent.Contains("CompanyNameArabic") == true);
        nameInput.Change("شركة الفجر للبرمجيات");

        // Upload file
        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("Contract PDF bytes", "contract.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        // 1. Company created first without document
        _handler.LastCreatedCompany.Should().NotBeNull();
        _handler.LastCreatedCompany!.CompanyName.Should().Be("شركة الفجر للبرمجيات");
        _handler.LastCreatedCompany.IncorporationDocumentId.Should().BeNull();

        // 2. Document uploaded using created company ID
        _handler.LastUploadedOwnerType.Should().Be(DocumentOwnerTypes.Company);
        _handler.LastUploadedOwnerId.Should().Be(_handler.CreatedCompanyId);

        // 3. Company updated with uploaded document ID
        _handler.LastUpdatedCompanyId.Should().Be(_handler.CreatedCompanyId);
        _handler.LastUpdatedCompany.Should().NotBeNull();
        _handler.LastUpdatedCompany!.IncorporationDocumentId.Should().Be(_handler.UploadedDocumentId);
    }

    [Fact]
    public async Task CompanyForm_CreateMode_WithoutFile_CreatesCompanyAndNavigates()
    {
        var cut = RenderComponent<CompanyForm>();

        var inputs = cut.FindAll("input.ios-input");
        var nameInput = inputs.First(i => i.ParentElement?.TextContent.Contains("CompanyNameArabic") == true);
        nameInput.Change("شركة النيل للمقاولات");

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.LastCreatedCompany.Should().NotBeNull();
        _handler.LastCreatedCompany!.CompanyName.Should().Be("شركة النيل للمقاولات");
        _handler.LastUploadedOwnerId.Should().BeNull();
        _handler.LastUpdatedCompanyId.Should().BeNull();
    }

    [Fact]
    public void CompanyForm_EditMode_LoadsExistingCompanyAndRendersFields()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        _handler.ExistingCompany = CreateCompanyDto(
            id: companyId,
            fileNumber: "COMP-77",
            category: CompanyCategory.Partnership,
            companyName: "شركة الأمل المتحدة",
            companyNameEn: "Al-Amal Co",
            tradeName: "الأمل",
            establishmentDate: new DateOnly(2020, 1, 1),
            registrationNumber: "12345",
            address: "شارع التحرير، القاهرة",
            incorporationDocumentId: docId
        );

        var cut = RenderComponent<CompanyForm>(p => p.Add(x => x.Id, companyId));

        cut.Find("h1.ios-title").TextContent.Should().Be("EditCompany");
        var nameInput = cut.FindAll("input.ios-input")
            .First(i => i.ParentElement?.TextContent.Contains("CompanyNameArabic") == true);
        nameInput.GetAttribute("value").Should().Be("شركة الأمل المتحدة");
    }

    [Fact]
    public async Task CompanyForm_EditMode_PreservesBulkPartnerDocumentsAndHasManyPartnersOnUpdate()
    {
        var companyId = Guid.NewGuid();
        var partnerIdsDocId = Guid.NewGuid();
        var partnerAttorneysDocId = Guid.NewGuid();

        _handler.ExistingCompany = CreateCompanyDto(
            id: companyId,
            fileNumber: "COMP-MANY",
            category: CompanyCategory.CapitalCompany,
            companyName: "شركة الشركاء المتعددين",
            registrationNumber: "8888",
            address: "العنوان القديم",
            hasManyPartners: true,
            partnersIdsDocumentId: partnerIdsDocId,
            partnersAttorneysDocumentId: partnerAttorneysDocId
        );

        var cut = RenderComponent<CompanyForm>(p => p.Add(x => x.Id, companyId));

        // Update address
        var addressInput = cut.Find("textarea.ios-input");
        addressInput.Change("العنوان الجديد بعد التعديل");

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.LastUpdatedCompanyId.Should().Be(companyId);
        _handler.LastUpdatedCompany.Should().NotBeNull();
        _handler.LastUpdatedCompany!.HasManyPartners.Should().BeTrue();
        _handler.LastUpdatedCompany.PartnersIdsDocumentId.Should().Be(partnerIdsDocId);
        _handler.LastUpdatedCompany.PartnersAttorneysDocumentId.Should().Be(partnerAttorneysDocId);
        _handler.LastUpdatedCompany.Address.Should().Be("العنوان الجديد بعد التعديل");
    }

    [Fact]
    public async Task CompanyForm_EditMode_WithNewIncorporationFile_ReplacesDocumentAndUpdatesCompany()
    {
        var companyId = Guid.NewGuid();
        var oldDocId = Guid.NewGuid();

        _handler.ExistingCompany = CreateCompanyDto(
            id: companyId,
            fileNumber: "COMP-DOC",
            category: CompanyCategory.SoleProprietorship,
            companyName: "مؤسسة الفردية",
            registrationNumber: "999",
            incorporationDocumentId: oldDocId
        );

        var cut = RenderComponent<CompanyForm>(p => p.Add(x => x.Id, companyId));

        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("New Contract bytes", "new-contract.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.ReplacedDocumentIds.Should().ContainSingle().Which.Should().Be(oldDocId);
        _handler.LastUpdatedCompanyId.Should().Be(companyId);
        _handler.LastUpdatedCompany.Should().NotBeNull();
        _handler.LastUpdatedCompany!.IncorporationDocumentId.Should().Be(oldDocId);
    }

    [Fact]
    public void CompanyForm_EditMode_WhenCompanyNotFound_ShowsErrorToast()
    {
        var companyId = Guid.NewGuid();
        _handler.ExistingCompany = null;

        var cut = RenderComponent<CompanyForm>(p => p.Add(x => x.Id, companyId));

        _mockToast.Verify(t => t.ShowError("CompanyNotFound"), Times.Once);
    }

    private static CompanyDto CreateCompanyDto(
        Guid id,
        string? fileNumber = null,
        CompanyCategory category = CompanyCategory.SoleProprietorship,
        string companyName = "شركة اختبار",
        string? companyNameEn = null,
        string? tradeName = null,
        DateOnly? establishmentDate = null,
        string? registrationNumber = null,
        string? address = null,
        Guid? incorporationDocumentId = null,
        DocumentDto? incorporationDocument = null,
        bool hasManyPartners = false,
        Guid? partnersIdsDocumentId = null,
        Guid? partnersAttorneysDocumentId = null)
    {
        return new CompanyDto(
            id,
            fileNumber,
            category,
            companyName,
            companyNameEn,
            tradeName,
            establishmentDate,
            registrationNumber,
            address,
            incorporationDocumentId,
            incorporationDocument,
            hasManyPartners,
            null,
            partnersIdsDocumentId,
            null,
            partnersAttorneysDocumentId,
            null,
            [],
            []
        );
    }

    private class CompanyFormRecordingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public Guid CreatedCompanyId { get; } = Guid.NewGuid();
        public Guid UploadedDocumentId { get; } = Guid.NewGuid();
        public CompanyDto? ExistingCompany { get; set; }

        public CreateCompanyDto? LastCreatedCompany { get; private set; }
        public Guid? LastUpdatedCompanyId { get; private set; }
        public UpdateCompanyDto? LastUpdatedCompany { get; private set; }
        public string? LastUploadedOwnerType { get; private set; }
        public Guid? LastUploadedOwnerId { get; private set; }
        public List<Guid> ReplacedDocumentIds { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var query = request.RequestUri?.Query ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/companies/", StringComparison.OrdinalIgnoreCase))
            {
                if (ExistingCompany == null)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ExistingCompany, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.Equals("/api/companies", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastCreatedCompany = JsonSerializer.Deserialize<CreateCompanyDto>(body, JsonOptions);

                var createdDto = CreateCompanyDto(
                    id: CreatedCompanyId,
                    fileNumber: LastCreatedCompany?.FileNumber,
                    category: LastCreatedCompany?.Category ?? CompanyCategory.SoleProprietorship,
                    companyName: LastCreatedCompany?.CompanyName ?? string.Empty,
                    companyNameEn: LastCreatedCompany?.CompanyNameEn,
                    tradeName: LastCreatedCompany?.TradeName,
                    establishmentDate: LastCreatedCompany?.EstablishmentDate,
                    registrationNumber: LastCreatedCompany?.RegistrationNumber,
                    address: LastCreatedCompany?.Address,
                    incorporationDocumentId: LastCreatedCompany?.IncorporationDocumentId,
                    hasManyPartners: LastCreatedCompany?.HasManyPartners ?? false
                );

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(createdDto, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Put && path.StartsWith("/api/companies/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var id))
                    LastUpdatedCompanyId = id;

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastUpdatedCompany = JsonSerializer.Deserialize<UpdateCompanyDto>(body, JsonOptions);

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

                var dto = new DocumentDto(UploadedDocumentId, "new-contract.pdf", "stored-new.pdf", "application/pdf", 2048);
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
