using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LegalERP.Application.Financials;

namespace LegalERP.Web.Services;

public class FinancialsApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FinancialsApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<GlobalFinancialsDto?> GetSummaryAsync()
    {
        return await _http.GetFromJsonAsync<GlobalFinancialsDto>("api/financials/summary", JsonOptions);
    }

    public async Task<EntityFinancialsDto?> GetFinancialsAsync(string ownerType, Guid ownerId)
    {
        var response = await _http.GetAsync($"api/financials/{ownerType.ToLowerInvariant()}/{ownerId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EntityFinancialsDto>(JsonOptions);
    }

    public async Task UpdateAgreedFeeAsync(string ownerType, Guid ownerId, UpdateAgreedFeeDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/financials/{ownerType.ToLowerInvariant()}/{ownerId}/agreed-fee", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task<FeeTransactionDto?> AddFeeTransactionAsync(string ownerType, Guid ownerId, AddFeeTransactionDto dto)
    {
        var response = await _http.PostAsJsonAsync($"api/financials/{ownerType.ToLowerInvariant()}/{ownerId}/transactions", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FeeTransactionDto>(JsonOptions);
    }

    public async Task DeleteFeeTransactionAsync(string ownerType, Guid ownerId, Guid transactionId)
    {
        var response = await _http.DeleteAsync($"api/financials/{ownerType.ToLowerInvariant()}/{ownerId}/transactions/{transactionId}");
        response.EnsureSuccessStatusCode();
    }
}
