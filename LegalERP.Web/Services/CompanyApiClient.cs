using LegalERP.Application.Companies;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegalERP.Web.Services;

public class CompanyApiClient
{
    private readonly HttpClient _http;


    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public CompanyApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<CompanyDto>> GetAllAsync()
    {
        var result = await _http.GetFromJsonAsync<List<CompanyDto>>("api/companies", JsonOptions);
        return result ?? new List<CompanyDto>();
    }

    public async Task<List<CompanyDto>> SearchAsync(string? searchTerm, LegalERP.Domain.Enums.CompanyCategory? category)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"term={Uri.EscapeDataString(searchTerm)}");
        if (category.HasValue)
            queryParams.Add($"category={category.Value}");

        var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var result = await _http.GetFromJsonAsync<List<CompanyDto>>($"api/companies/search{queryString}", JsonOptions);
        return result ?? new List<CompanyDto>();
    }

    public async Task<CompanyDto?> CreateAsync(CreateCompanyDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/companies", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompanyDto>(JsonOptions);
    }

    public async Task<CompanyDto?> GetByIdAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<CompanyDto>($"api/companies/{id}", JsonOptions);
    }

    public async Task UpdateAsync(Guid id, UpdateCompanyDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/companies/{id}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/companies/{id}");
        response.EnsureSuccessStatusCode();
    }

    // --- Amendment methods ---

    public async Task<CompanyAmendmentDto?> AddAmendmentAsync(Guid companyId, CreateCompanyAmendmentDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/companies/{companyId}/amendments", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompanyAmendmentDto>(JsonOptions);
    }

    public async Task UpdateAmendmentAsync(Guid companyId, Guid amendmentId, UpdateCompanyAmendmentDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/companies/{companyId}/amendments/{amendmentId}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAmendmentAsync(Guid companyId, Guid amendmentId)
    {
        var response = await _http.DeleteAsync($"api/companies/{companyId}/amendments/{amendmentId}");
        response.EnsureSuccessStatusCode();
    }

    // --- Partner methods ---

    public async Task<CompanyPartnerDto?> AddPartnerAsync(Guid companyId, CreateCompanyPartnerDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/companies/{companyId}/partners", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompanyPartnerDto>(JsonOptions);
    }

    public async Task UpdatePartnerAsync(Guid companyId, Guid partnerId, UpdateCompanyPartnerDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/companies/{companyId}/partners/{partnerId}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePartnerAsync(Guid companyId, Guid partnerId)
    {
        var response = await _http.DeleteAsync($"api/companies/{companyId}/partners/{partnerId}");
        response.EnsureSuccessStatusCode();
    }

    // --- Document methods ---

    public async Task<List<DocumentDto>> GetDocumentsAsync(string ownerType, Guid ownerId)
    {
        var result = await _http.GetFromJsonAsync<List<DocumentDto>>($"api/documents?ownerType={ownerType}&ownerId={ownerId}", JsonOptions);
        return result ?? new List<DocumentDto>();
    }

    public async Task<Guid> UploadDocumentAsync(string ownerType, Guid ownerId, Microsoft.AspNetCore.Components.Forms.IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(100 * 1024 * 1024); // max 100MB
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        
        content.Add(streamContent, "file", file.Name);

        var response = await _http.PostAsync($"api/documents/upload?ownerType={ownerType}&ownerId={ownerId}", content);
        response.EnsureSuccessStatusCode();
        
        var idStr = await response.Content.ReadAsStringAsync();
        return Guid.Parse(idStr.Trim('"'));
    }

    public async Task DeleteDocumentAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/documents/{id}");
        response.EnsureSuccessStatusCode();
    }
}