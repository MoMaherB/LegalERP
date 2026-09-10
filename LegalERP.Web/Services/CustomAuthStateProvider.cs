using System.Security.Claims;
using LegalERP.Application.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace LegalERP.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthApiClient _authApi;
    private CurrentUserDto? _cachedUser;
    private bool _initialized = false;

    public CustomAuthStateProvider(IHttpContextAccessor httpContextAccessor, AuthApiClient authApi)
    {
        _httpContextAccessor = httpContextAccessor;
        _authApi = authApi;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var idStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Guid.TryParse(idStr, out var id);
                _cachedUser = new CurrentUserDto(
                    id,
                    user.FindFirst(ClaimTypes.Name)?.Value ?? "",
                    user.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    user.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer",
                    user.FindFirst("ProfilePicturePath")?.Value
                );
            }
            else
            {
                try
                {
                    _cachedUser = await _authApi.GetCurrentUserAsync();
                }
                catch
                {
                    _cachedUser = null;
                }
            }
            _initialized = true;
        }

        return BuildState(_cachedUser);
    }

    public Task NotifyUserLoggedIn(CurrentUserDto userDto)
    {
        _cachedUser = userDto;
        _initialized = true;
        NotifyAuthenticationStateChanged(Task.FromResult(BuildState(userDto)));
        return Task.CompletedTask;
    }

    public void NotifyUserLoggedOut()
    {
        _cachedUser = null;
        _initialized = true;
        NotifyAuthenticationStateChanged(Task.FromResult(BuildState(null)));
    }

    public CurrentUserDto? GetCachedUser() => _cachedUser;

    private static AuthenticationState BuildState(CurrentUserDto? user)
    {
        if (user == null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var identity = new ClaimsIdentity(claims, "CookieAuth");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
