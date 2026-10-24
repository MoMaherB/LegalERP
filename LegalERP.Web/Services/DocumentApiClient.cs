using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegalERP.Application.Companies;
using Microsoft.AspNetCore.Components.Forms;

namespace LegalERP.Web.Services;

public class DocumentApiClient
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB

    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DocumentApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(string ownerType, Guid ownerId)
    {
        var result = await _http.GetFromJsonAsync<List<DocumentDto>>(
            $"api/documents?ownerType={Uri.EscapeDataString(ownerType)}&ownerId={ownerId}",
            JsonOptions);
        return result ?? new List<DocumentDto>();
    }

    public async Task<Guid> UploadDocumentAsync(string ownerType, Guid ownerId, IBrowserFile file, string? title = null)
    {
        using var content = CreateMultipartFileContent(file, out var fileStream);
        using (fileStream)
        {
            var url = $"api/documents/upload?ownerType={Uri.EscapeDataString(ownerType)}&ownerId={ownerId}";
            if (!string.IsNullOrWhiteSpace(title))
            {
                url += $"&title={Uri.EscapeDataString(title)}";
            }

            var response = await _http.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var idStr = await response.Content.ReadAsStringAsync();
            return Guid.Parse(idStr.Trim('"'));
        }
    }

    public async Task<DocumentDto?> ReplaceDocumentAsync(Guid documentId, IBrowserFile file, string? title = null)
    {
        using var content = CreateMultipartFileContent(file, out var fileStream);
        using (fileStream)
        {
            var url = $"api/documents/{documentId}/replace";
            if (!string.IsNullOrWhiteSpace(title))
            {
                url += $"?title={Uri.EscapeDataString(title)}";
            }

            var response = await _http.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DocumentDto>(JsonOptions);
        }
    }

    public async Task DeleteDocumentAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/documents/{id}");
        response.EnsureSuccessStatusCode();
    }

    private static MultipartFormDataContent CreateMultipartFileContent(IBrowserFile file, out Stream fileStream)
    {
        var content = new MultipartFormDataContent();
        fileStream = file.OpenReadStream(MaxFileSizeBytes);
        var streamContent = new StreamContent(fileStream);
        var mediaType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Add(streamContent, "file", file.Name);
        return content;
    }
}
