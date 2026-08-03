using System;
using System.Linq;
using System.Threading.Tasks;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalERP.Infrastructure.Services;

public class HearingReminderJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HearingReminderJob> _logger;

    public HearingReminderJob(IServiceScopeFactory scopeFactory, ILogger<HearingReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("HearingReminderJob started at {Time}", DateTime.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pushService = scope.ServiceProvider.GetRequiredService<WebPushNotificationService>();

        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

        var hearings = await db.CaseHearings
            .Include(h => h.Case)
            .Where(h => !h.IsDeleted && h.HearingDate == tomorrow)
            .ToListAsync();

        _logger.LogInformation("Found {Count} hearings scheduled for tomorrow ({Date})", hearings.Count, tomorrow);

        foreach (var hearing in hearings)
        {
            var caseName = hearing.Case?.Title ?? "Unknown Case";
            var caseNumber = hearing.Case?.CaseNumber ?? "N/A";

            var title = $"📅 Hearing Tomorrow - Case #{caseNumber}";
            var message = $"You have a court hearing tomorrow ({tomorrow:yyyy-MM-dd}) for case \"{caseName}\".";

            if (!string.IsNullOrEmpty(hearing.Purpose))
            {
                message += $" Purpose: {hearing.Purpose}.";
            }

            // Save notification to database
            var notification = new Notification
            {
                Title = title,
                Message = message,
                CaseId = hearing.CaseId
            };

            await db.Notifications.AddAsync(notification);

            // Send Web Push
            try
            {
                await pushService.SendToAllSubscribersAsync(title, message, hearing.CaseId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification for hearing {HearingId}", hearing.Id);
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("HearingReminderJob finished. {Count} notifications created.", hearings.Count);
    }
}
