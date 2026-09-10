using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LegalERP.Web.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;

    public AuthHeaderHandler(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CurrentUserDto? user = null;

        try
        {
            var authState = _serviceProvider.GetService<CustomAuthStateProvider>();
            user = authState?.GetCachedUser();
        }
        catch
        {
            // Ignore if provider cannot be resolved yet
        }

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
