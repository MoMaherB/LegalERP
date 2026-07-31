using LegalERP.Application.Cases;
using LegalERP.Domain.Enums;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegalERP.Web.Services;

public class CaseApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CaseApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<CaseDto>> GetAllAsync()
    {
        var result = await _http.GetFromJsonAsync<List<CaseDto>>("api/cases", JsonOptions);
        return result ?? new List<CaseDto>();
    }

    public async Task<List<CaseDto>> SearchAsync(string? searchTerm, CaseType? type, CaseStatus? status)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"term={Uri.EscapeDataString(searchTerm)}");
        if (type.HasValue)
            queryParams.Add($"type={type.Value}");
        if (status.HasValue)
            queryParams.Add($"status={status.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var result = await _http.GetFromJsonAsync<List<CaseDto>>($"api/cases/search{queryString}", JsonOptions);
        return result ?? new List<CaseDto>();
    }

    public async Task<CaseDto?> GetByIdAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<CaseDto>($"api/cases/{id}", JsonOptions);
    }

    public async Task<CaseDto?> CreateAsync(CreateCaseDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/cases", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CaseDto>(JsonOptions);
    }

    public async Task UpdateAsync(Guid id, UpdateCaseDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/cases/{id}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/cases/{id}");
        response.EnsureSuccessStatusCode();
    }

    // --- Case Party methods ---

    public async Task<CasePartyDto?> AddPartyAsync(Guid caseId, CreateCasePartyDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/cases/{caseId}/parties", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CasePartyDto>(JsonOptions);
    }

    public async Task UpdatePartyAsync(Guid caseId, Guid partyId, UpdateCasePartyDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/cases/{caseId}/parties/{partyId}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePartyAsync(Guid caseId, Guid partyId)
    {
        var response = await _http.DeleteAsync($"api/cases/{caseId}/parties/{partyId}");
        response.EnsureSuccessStatusCode();
    }
}
