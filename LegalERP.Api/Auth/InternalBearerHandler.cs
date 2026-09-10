using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using LegalERP.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegalERP.Api.Auth;

public class InternalBearerHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public InternalBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var user = InternalTokenHelper.ValidateToken(token);
        if (user == null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal bearer token"));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };

        var identity = new ClaimsIdentity(claims, "InternalBearer");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "InternalBearer");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
