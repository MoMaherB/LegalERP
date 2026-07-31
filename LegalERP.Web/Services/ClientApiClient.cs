using LegalERP.Application.Clients;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LegalERP.Web.Services;

public class ClientApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ClientApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<ClientSummaryDto>> GetAllAsync()
    {
        var result = await _http.GetFromJsonAsync<List<ClientSummaryDto>>("api/clients", JsonOptions);
        return result ?? new();
    }

    public async Task<List<ClientSummaryDto>> SearchAsync(string? term)
    {
        var url = $"api/clients/search?term={Uri.EscapeDataString(term ?? "")}";
        var result = await _http.GetFromJsonAsync<List<ClientSummaryDto>>(url, JsonOptions);
        return result ?? new();
    }

    public async Task<ClientDto?> GetByIdAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<ClientDto>($"api/clients/{id}", JsonOptions);
    }

    public async Task<ClientSummaryDto?> CreateAsync(CreateClientDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/clients", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClientSummaryDto>(JsonOptions);
    }

    public async Task UpdateAsync(Guid id, UpdateClientDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/clients/{id}", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/clients/{id}");
        response.EnsureSuccessStatusCode();
    }
}
