using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Auth;
using Microsoft.AspNetCore.Http;

namespace LegalERP.Web.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CustomAuthStateProvider _authState;

    public AuthHeaderHandler(IHttpContextAccessor httpContextAccessor, CustomAuthStateProvider authState)
    {
        _httpContextAccessor = httpContextAccessor;
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var user = _authState.GetCachedUser();
        if (user != null)
        {
            var token = InternalTokenHelper.GenerateToken(user);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            var httpUser = _httpContextAccessor.HttpContext?.User;
            if (httpUser?.Identity?.IsAuthenticated == true)
            {
                var idStr = httpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                Guid.TryParse(idStr, out var id);
                var currentUser = new CurrentUserDto(
                    id,
                    httpUser.FindFirst(ClaimTypes.Name)?.Value ?? "",
                    httpUser.FindFirst(ClaimTypes.Email)?.Value ?? "",
                    httpUser.FindFirst(ClaimTypes.Role)?.Value ?? "Viewer",
                    httpUser.FindFirst("ProfilePicturePath")?.Value
                );
                var token = InternalTokenHelper.GenerateToken(currentUser);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
