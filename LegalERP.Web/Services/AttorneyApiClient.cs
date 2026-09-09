using LegalERP.Application.Attorneys;
using LegalERP.Application.Companies;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegalERP.Web.Services;

public class AttorneyApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AttorneyApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<AttorneySummaryDto>> GetAllAsync()
    {
        var result = await _http.GetFromJsonAsync<List<AttorneySummaryDto>>("api/attorneys", JsonOptions);
        return result ?? new();
    }

    public async Task<List<AttorneySummaryDto>> SearchAsync(string? term)
    {
        var url = $"api/attorneys/search?term={Uri.EscapeDataString(term ?? "")}";
        var result = await _http.GetFromJsonAsync<List<AttorneySummaryDto>>(url, JsonOptions);
        return result ?? new();
    }

    public async Task<AttorneyDto?> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"api/attorneys/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttorneyDto>(JsonOptions);
    }

    public async Task<List<ClientAttorneyDto>> GetByClientIdAsync(Guid clientId)
    {
        var result = await _http.GetFromJsonAsync<List<ClientAttorneyDto>>($"api/attorneys/by-client/{clientId}", JsonOptions);
        return result ?? new();
    }

    public async Task<AttorneySummaryDto?> CreateAsync(CreateAttorneyDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/attorneys", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttorneySummaryDto>(JsonOptions);
    }

    public async Task UpdateAsync(Guid id, UpdateAttorneyDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/attorneys/{id}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/attorneys/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task AddClientAsync(Guid attorneyId, AddAttorneyClientDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/attorneys/{attorneyId}/clients", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveClientAsync(Guid attorneyId, Guid linkId)
    {
        var response = await _http.DeleteAsync($"api/attorneys/{attorneyId}/clients/{linkId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UploadDocumentAsync(Guid attorneyId, Microsoft.AspNetCore.Components.Forms.IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(100 * 1024 * 1024); // max 100MB
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(streamContent, "file", file.Name);

        var url = $"api/documents/upload?ownerType=Attorney&ownerId={attorneyId}";
        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var idStr = await response.Content.ReadAsStringAsync();
        return Guid.Parse(idStr.Trim('"'));
    }
}
