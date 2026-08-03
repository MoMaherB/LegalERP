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
}
