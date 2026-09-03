using System.Net.Http.Json;
using LegalERP.Application.Auth;

namespace LegalERP.Web.Services;

public class AuthApiClient
{
    private readonly HttpClient _http;

    public AuthApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("LegalErpApi");
    }

    public async Task<CurrentUserDto?> LoginAsync(LoginDto dto)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", dto);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH API CLIENT ERROR] {ex.Message}");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        await _http.PostAsync("api/auth/logout", null);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/auth/me");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return null;
            return await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/profile/change-password", dto);
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> UploadProfilePictureAsync(Stream fileStream, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync("api/profile/upload-picture", form);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<UploadPictureResult>();
        return result?.ProfilePicturePath;
    }

    private record UploadPictureResult(string ProfilePicturePath);
}
