using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace LegalERP.Infrastructure.Services;

public class WebPushNotificationService
{
    private readonly IConfiguration _config;
    private readonly ILogger<WebPushNotificationService> _logger;
    private readonly ApplicationDbContext _db;

    public WebPushNotificationService(
        IConfiguration config,
        ILogger<WebPushNotificationService> logger,
        ApplicationDbContext db)
    {
        _config = config;
        _logger = logger;
        _db = db;
    }

    public async Task SendToAllSubscribersAsync(string title, string message, Guid? caseId = null)
    {
        var vapidSubject = _config["VapidKeys:Subject"] ?? "mailto:admin@legalerp.com";
        var vapidPublicKey = _config["VapidKeys:PublicKey"];
        var vapidPrivateKey = _config["VapidKeys:PrivateKey"];

        if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
        {
            _logger.LogWarning("VAPID keys not configured. Skipping Web Push.");
            return;
        }

        var webPushClient = new WebPushClient();
        var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);

        var subscriptions = await _db.PushSubscriptions
            .Where(s => !s.IsDeleted)
            .ToListAsync();

        if (subscriptions.Count == 0)
        {
            _logger.LogInformation("No push subscriptions found. Skipping Web Push.");
            return;
        }

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            body = message,
            url = caseId.HasValue ? $"/cases/{caseId}" : "/notifications"
        });

        var expiredSubscriptions = new List<LegalERP.Domain.Entities.PushSubscription>();

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
                _logger.LogInformation("Push sent to {Endpoint}", sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                                                ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Subscription expired/invalid, removing: {Endpoint}", sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
                expiredSubscriptions.Add(sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push to {Endpoint}", sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
            }
        }

        // Clean up expired subscriptions
        if (expiredSubscriptions.Count > 0)
        {
            _db.PushSubscriptions.RemoveRange(expiredSubscriptions);
            await _db.SaveChangesAsync();
        }
    }
}
