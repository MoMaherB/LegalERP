using System.Net.Http.Json;
using LegalERP.Application.Auth;

namespace LegalERP.Web.Services;

public class UserApiClient
{
    private readonly HttpClient _http;

    public UserApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<UserDto>>("api/users") ?? new();
    }

    public async Task<(bool Success, string Message)> CreateAsync(CreateUserDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/users", dto);
        var body = await response.Content.ReadFromJsonAsync<ApiMessageResult>();
        return (response.IsSuccessStatusCode, body?.Message ?? "خطأ غير معروف");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/users/{id}", dto);
        var body = await response.Content.ReadFromJsonAsync<ApiMessageResult>();
        return (response.IsSuccessStatusCode, body?.Message ?? "خطأ غير معروف");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(Guid id, string newPassword)
    {
        var response = await _http.PostAsJsonAsync($"api/users/{id}/reset-password", new ResetUserPasswordDto(newPassword));
        var body = await response.Content.ReadFromJsonAsync<ApiMessageResult>();
        return (response.IsSuccessStatusCode, body?.Message ?? "خطأ غير معروف");
    }

    public async Task<(bool Success, string Message)> DeactivateAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/users/{id}");
        var body = await response.Content.ReadFromJsonAsync<ApiMessageResult>();
        return (response.IsSuccessStatusCode, body?.Message ?? "خطأ غير معروف");
    }

    public async Task<(bool Success, string Message)> ReactivateAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/users/{id}/reactivate", null);
        var body = await response.Content.ReadFromJsonAsync<ApiMessageResult>();
        return (response.IsSuccessStatusCode, body?.Message ?? "خطأ غير معروف");
    }

    public async Task<(bool Success, string Message)> DeletePermanentAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"api/users/{id}/permanent");
        var body = await response.Content.ReadFromJsonAsync<ApiMessageResult>();
        return (response.IsSuccessStatusCode, body?.Message ?? "خطأ غير معروف");
    }

    private record ApiMessageResult(string Message);
}
