using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using LegalERP.Api.Common.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LegalERP.Tests.Infrastructure;

public class RateLimitingTests
{
    private static async Task<IHost> CreateTestHostAsync(Action<IConfigurationBuilder>? configureConfig = null)
    {
        var host = new HostBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                configureConfig?.Invoke(config);
            })
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddRouting();
                        services.AddAuthentication("Test")
                            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                        services.AddAuthorization();
                        services.AddAppRateLimiting(ctx.Configuration);
                    })
                    .Configure(app =>
                    {
                        // Helper test middleware to set simulated remote IP
                        app.Use((context, next) =>
                        {
                            if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var testIp) &&
                                IPAddress.TryParse(testIp, out var parsed))
                            {
                                context.Connection.RemoteIpAddress = parsed;
                            }
                            return next();
                        });

                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseRateLimiter();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost("/api/auth/login", () => Results.Ok(new { message = "Logged in" }))
                                .RequireRateLimiting(RateLimitingConstants.AuthLoginPolicy);

                            endpoints.MapGet("/api/cases", () => Results.Ok(new { message = "Cases list" }))
                                .RequireRateLimiting(RateLimitingConstants.GeneralApiPolicy);
                        });
                    });
            });

        return await host.StartAsync();
    }

    [Fact]
    public async Task AuthLogin_WhenRemoteIpExceedsLimit_Returns429WithLocalizedMessageAndRetryAfter()
    {
        using var host = await CreateTestHostAsync(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RateLimiting:Auth:PermitLimit", "2" },
                { "RateLimiting:Auth:WindowSeconds", "60" },
                { "RateLimiting:BypassLoopback", "false" }
            });
        });

        var client = host.GetTestServer().CreateClient();

        HttpRequestMessage CreateReq(string? acceptLang = null)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "user@test.com" })
            };
            req.Headers.Add("X-Test-Remote-IP", "198.51.100.5");
            if (!string.IsNullOrEmpty(acceptLang))
            {
                req.Headers.Add("Accept-Language", acceptLang);
            }
            return req;
        }

        // 1st request -> 200 OK
        (await client.SendAsync(CreateReq())).StatusCode.Should().Be(HttpStatusCode.OK);

        // 2nd request -> 200 OK
        (await client.SendAsync(CreateReq())).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3rd request -> 429 Too Many Requests
        var res3 = await client.SendAsync(CreateReq());
        res3.StatusCode.Should().Be((HttpStatusCode)429);

        // Verify Arabic message by default
        var content = await res3.Content.ReadAsStringAsync();
        content.Should().Contain("تم تجاوز");

        // Verify Retry-After header
        res3.Headers.Contains("Retry-After").Should().BeTrue();
        res3.Headers.GetValues("Retry-After").First().Should().Be("60");

        // 4th request with English header returns English error message
        var resEnglish = await client.SendAsync(CreateReq("en-US"));
        resEnglish.StatusCode.Should().Be((HttpStatusCode)429);
        var contentEnglish = await resEnglish.Content.ReadAsStringAsync();
        contentEnglish.Should().Contain("Too many requests");
    }

    [Fact]
    public async Task AuthLogin_WhenLoopbackBypassEnabled_AllowsRequestsPastLimit()
    {
        using var host = await CreateTestHostAsync(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RateLimiting:Auth:PermitLimit", "2" },
                { "RateLimiting:Auth:WindowSeconds", "60" },
                { "RateLimiting:BypassLoopback", "true" }
            });
        });

        var client = host.GetTestServer().CreateClient();

        for (int i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "user@test.com" })
            };
            req.Headers.Add("X-Test-Remote-IP", "127.0.0.1");
            var res = await client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task AuthLogin_WhenInternalServerBypassKeyProvided_AllowsRequestsPastLimit()
    {
        const string bypassKey = "secret-internal-token-123";
        using var host = await CreateTestHostAsync(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RateLimiting:Auth:PermitLimit", "2" },
                { "RateLimiting:Auth:WindowSeconds", "60" },
                { "RateLimiting:BypassLoopback", "false" },
                { "RateLimiting:InternalBypassKey", bypassKey }
            });
        });

        var client = host.GetTestServer().CreateClient();

        for (int i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "user@test.com" })
            };
            req.Headers.Add("X-Test-Remote-IP", "203.0.113.10"); // External IP
            req.Headers.Add(RateLimitingExtensions.InternalBypassHeader, bypassKey);

            var res = await client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task GeneralApi_WhenLimitExceeded_Returns429()
    {
        using var host = await CreateTestHostAsync(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RateLimiting:General:PermitLimit", "3" },
                { "RateLimiting:General:WindowSeconds", "60" },
                { "RateLimiting:BypassLoopback", "false" }
            });
        });

        var client = host.GetTestServer().CreateClient();

        HttpRequestMessage CreateReq()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/api/cases");
            req.Headers.Add("X-Test-Remote-IP", "198.51.100.20");
            return req;
        }

        for (int i = 0; i < 3; i++)
        {
            (await client.SendAsync(CreateReq())).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 4th request exceeds limit
        var resExceeded = await client.SendAsync(CreateReq());
        resExceeded.StatusCode.Should().Be((HttpStatusCode)429);
    }

    [Fact]
    public async Task AuthLogin_WhenClientAExceedsLimit_ClientBIsNotThrottled()
    {
        using var host = await CreateTestHostAsync(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RateLimiting:Auth:PermitLimit", "2" },
                { "RateLimiting:Auth:WindowSeconds", "60" },
                { "RateLimiting:BypassLoopback", "false" }
            });
        });

        var client = host.GetTestServer().CreateClient();

        HttpRequestMessage CreateRequest(string ip)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "user@test.com" })
            };
            req.Headers.Add("X-Test-Remote-IP", ip);
            return req;
        }

        // Client A reaches limit
        (await client.SendAsync(CreateRequest("198.51.100.1"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.SendAsync(CreateRequest("198.51.100.1"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.SendAsync(CreateRequest("198.51.100.1"))).StatusCode.Should().Be((HttpStatusCode)429);

        // Client B sends 1st request -> 200 OK
        (await client.SendAsync(CreateRequest("198.51.100.2"))).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
