using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalERP.Api.Common.RateLimiting;

public static class RateLimitingExtensions
{
    public const string InternalBypassHeader = "X-Internal-Bypass-Key";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var rateLimitOptions = new RateLimitingOptions();
        if (configuration != null)
        {
            var section = configuration.GetSection(RateLimitingOptions.SectionName);
            section.Bind(rateLimitOptions);

            if (int.TryParse(section["AuthPermitLimit"], out var authLimit) && authLimit > 0)
                rateLimitOptions.Auth.PermitLimit = authLimit;
            if (int.TryParse(section["AuthWindowSeconds"], out var authWindow) && authWindow > 0)
                rateLimitOptions.Auth.WindowSeconds = authWindow;
            if (int.TryParse(section["GeneralPermitLimit"], out var genLimit) && genLimit > 0)
                rateLimitOptions.General.PermitLimit = genLimit;
            if (int.TryParse(section["GeneralWindowSeconds"], out var genWindow) && genWindow > 0)
                rateLimitOptions.General.WindowSeconds = genWindow;
            if (bool.TryParse(section["BypassLoopback"], out var bypass))
                rateLimitOptions.BypassLoopback = bypass;
            if (!string.IsNullOrWhiteSpace(section["InternalBypassKey"]))
                rateLimitOptions.InternalBypassKey = section["InternalBypassKey"];
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

                var retrySeconds = rateLimitOptions.Auth.WindowSeconds;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retrySeconds = Math.Max(1, (int)retryAfter.TotalSeconds);
                }

                context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString();

                var acceptLang = context.HttpContext.Request.Headers.AcceptLanguage.ToString();
                var isEnglish = acceptLang.StartsWith("en", StringComparison.OrdinalIgnoreCase);

                var message = isEnglish
                    ? "Too many requests. Please try again later."
                    : "تم تجاوز عدد الطلبات المسموح به. يرجى المحاولة بعد قليل.";

                var payload = new
                {
                    message,
                    retryAfterSeconds = retrySeconds,
                    statusCode = StatusCodes.Status429TooManyRequests
                };

                await context.HttpContext.Response.WriteAsJsonAsync(payload, cancellationToken);
            };

            // 1. Strict Policy for /api/auth/login
            options.AddPolicy(RateLimitingConstants.AuthLoginPolicy, httpContext =>
            {
                if (IsBypassed(httpContext, rateLimitOptions))
                {
                    return RateLimitPartition.GetNoLimiter("bypassed");
                }

                var key = GetClientPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.Auth.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.Auth.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            // 2. General API Policy for public and authenticated endpoints
            options.AddPolicy(RateLimitingConstants.GeneralApiPolicy, httpContext =>
            {
                if (IsBypassed(httpContext, rateLimitOptions))
                {
                    return RateLimitPartition.GetNoLimiter("bypassed");
                }

                var key = httpContext.User.Identity?.IsAuthenticated == true
                    ? $"user:{httpContext.User.Identity.Name}"
                    : $"ip:{GetClientPartitionKey(httpContext)}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitOptions.General.PermitLimit,
                    Window = TimeSpan.FromSeconds(rateLimitOptions.General.WindowSeconds),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
        });

        return services;
    }

    private static bool IsBypassed(HttpContext context, RateLimitingOptions options)
    {
        // 1. Internal server-to-server shared secret header bypass
        if (!string.IsNullOrWhiteSpace(options.InternalBypassKey) &&
            context.Request.Headers.TryGetValue(InternalBypassHeader, out var keyHeader) &&
            keyHeader == options.InternalBypassKey)
        {
            return true;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            return options.BypassLoopback;
        }

        // 2. Local loopback bypass
        if (options.BypassLoopback && IPAddress.IsLoopback(remoteIp))
        {
            return true;
        }

        // 3. Whitelisted IP bypass
        if (options.WhitelistedIps.Count > 0 && options.WhitelistedIps.Contains(remoteIp.ToString()))
        {
            return true;
        }

        return false;
    }

    private static string GetClientPartitionKey(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        return remoteIp?.ToString() ?? "unknown_client";
    }
}
