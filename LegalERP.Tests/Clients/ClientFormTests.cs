using Bunit;
using FluentAssertions;
using LegalERP.Application.Clients;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Clients;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace LegalERP.Tests.Clients;

public class ClientFormTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly ClientFormRecordingHandler _handler = new();

    public ClientFormTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_handler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new ClientApiClient(_mockHttpFactory.Object));
        Services.AddSingleton(new DocumentApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public async Task ClientForm_CreateWithNationalIdFile_CreatesClientFirstThenUploadsWithCreatedClientId()
    {
        var cut = RenderComponent<ClientForm>();

        cut.Find("input[placeholder='FullNameArabicPlaceholder']").Change("أحمد محمود");
        cut.Find("input[placeholder='NationalIdNumberPlaceholder']").Change("29001011234567");

        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("ID scan content", "id-card.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        // 1. Client created first without document
        _handler.LastCreatedClient.Should().NotBeNull();
        _handler.LastCreatedClient!.FullName.Should().Be("أحمد محمود");
        _handler.LastCreatedClient.NationalIdDocumentId.Should().BeNull();

        // 2. Document uploaded using created client's ID (not a random Guid)
        _handler.LastUploadedOwnerType.Should().Be(DocumentOwnerTypes.Client);
        _handler.LastUploadedOwnerId.Should().Be(_handler.CreatedClientId);

        // 3. Client updated with uploaded document ID
        _handler.LastUpdatedClientId.Should().Be(_handler.CreatedClientId);
        _handler.LastUpdatedClient.Should().NotBeNull();
        _handler.LastUpdatedClient!.NationalIdDocumentId.Should().Be(_handler.UploadedDocumentId);
    }

    [Fact]
    public async Task ClientForm_EditWithExistingNationalIdFile_ReplacesDocumentInPlaceViaDocumentApi()
    {
        var clientId = Guid.NewGuid();
        var existingDocId = Guid.NewGuid();
        _handler.ExistingClient = new ClientDto(
            Id: clientId,
            FileNumber: "CL-10",
            FullName: "سارة علي",
            FullNameEn: "Sara Ali",
            NationalIdNumber: "29502021234567",
            PhoneNumber: "01000000000",
            Email: null,
            Address: null,
            Notes: null,
            NationalIdDocument: new DocumentDto(existingDocId, "old-id.pdf", "stored-old.pdf", "application/pdf", 1024),
            RelatedCases: new List<ClientCaseDto>(),
            RelatedCompanies: new List<ClientCompanyDto>()
        );

        var cut = RenderComponent<ClientForm>(p => p.Add(x => x.Id, clientId));

        var inputFile = cut.FindComponent<InputFile>();
        var fileContent = InputFileContent.CreateFromText("New ID scan", "new-id.pdf", null, "application/pdf");
        inputFile.UploadFiles(fileContent);

        var form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        _handler.ReplacedDocumentIds.Should().ContainSingle().Which.Should().Be(existingDocId);
        _handler.LastUploadedOwnerId.Should().BeNull();
        _handler.LastUpdatedClientId.Should().Be(clientId);
        _handler.LastUpdatedClient.Should().NotBeNull();
        _handler.LastUpdatedClient!.NationalIdDocumentId.Should().Be(existingDocId);
    }

    private class ClientFormRecordingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public Guid CreatedClientId { get; } = Guid.NewGuid();
        public Guid UploadedDocumentId { get; } = Guid.NewGuid();
        public ClientDto? ExistingClient { get; set; }

        public CreateClientDto? LastCreatedClient { get; private set; }
        public Guid? LastUpdatedClientId { get; private set; }
        public UpdateClientDto? LastUpdatedClient { get; private set; }
        public string? LastUploadedOwnerType { get; private set; }
        public Guid? LastUploadedOwnerId { get; private set; }
        public List<Guid> ReplacedDocumentIds { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var query = request.RequestUri?.Query ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.StartsWith("/api/clients/", StringComparison.OrdinalIgnoreCase))
            {
                if (ExistingClient == null)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(ExistingClient, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Post && path.Equals("/api/clients", StringComparison.OrdinalIgnoreCase))
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastCreatedClient = JsonSerializer.Deserialize<CreateClientDto>(body, JsonOptions);

                var createdDto = new ClientDto(
                    Id: CreatedClientId,
                    FileNumber: LastCreatedClient?.FileNumber,
                    FullName: LastCreatedClient?.FullName ?? string.Empty,
                    FullNameEn: LastCreatedClient?.FullNameEn,
                    NationalIdNumber: LastCreatedClient?.NationalIdNumber,
                    PhoneNumber: LastCreatedClient?.PhoneNumber,
                    Email: LastCreatedClient?.Email,
                    Address: LastCreatedClient?.Address,
                    Notes: LastCreatedClient?.Notes,
                    NationalIdDocument: null,
                    RelatedCases: new List<ClientCaseDto>(),
                    RelatedCompanies: new List<ClientCompanyDto>()
                );

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(createdDto, JsonOptions), Encoding.UTF8, "application/json")
                };
            }

            if (request.Method == HttpMethod.Put && path.StartsWith("/api/clients/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var id))
                    LastUpdatedClientId = id;

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                LastUpdatedClient = JsonSerializer.Deserialize<UpdateClientDto>(body, JsonOptions);

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

                var dto = new DocumentDto(UploadedDocumentId, "new-id.pdf", "stored-new.pdf", "application/pdf", 2048);
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
