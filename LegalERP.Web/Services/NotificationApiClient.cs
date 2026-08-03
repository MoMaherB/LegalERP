using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LegalERP.Application.Notifications;

namespace LegalERP.Web.Services;

public class NotificationApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public NotificationApiClient(IHttpClientFactory factory) => _http = factory.CreateClient("LegalErpApi");

    public async Task<List<NotificationDto>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<NotificationDto>>("api/notifications", JsonOptions)
               ?? new List<NotificationDto>();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var result = await _http.GetFromJsonAsync<UnreadCountDto>("api/notifications/unread-count", JsonOptions);
        return result?.Count ?? 0;
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var response = await _http.PutAsync($"api/notifications/{id}/read", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllAsReadAsync()
    {
        var response = await _http.PutAsync("api/notifications/read-all", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetVapidPublicKeyAsync()
    {
        try
        {
            return await _http.GetStringAsync("api/notifications/vapid-public-key");
        }
        catch
        {
            return null;
        }
    }

    public async Task SubscribeAsync(CreatePushSubscriptionDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/notifications/subscribe", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
    }
}
