using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LegalERP.Application.Backups;

namespace LegalERP.Web.Services;

public class BackupApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public BackupApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<BackupRecordDto>> GetHistoryAsync()
    {
        var result = await _http.GetFromJsonAsync<List<BackupRecordDto>>("api/backups", JsonOptions);
        return result ?? new List<BackupRecordDto>();
    }

    public async Task<BackupStatusSummaryDto?> GetSummaryAsync()
    {
        return await _http.GetFromJsonAsync<BackupStatusSummaryDto>("api/backups/summary", JsonOptions);
    }

    public async Task<BackupResultDto?> ExportArchiveAsync()
    {
        var response = await _http.PostAsync("api/backups/export-archive", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BackupResultDto>(JsonOptions);
    }

    public string GetDownloadSnapshotUrl()
    {
        return "/api/admin/backups/download-snapshot";
    }
}
