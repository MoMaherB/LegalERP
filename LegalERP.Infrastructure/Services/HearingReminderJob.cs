using System;
using System.Linq;
using System.Threading.Tasks;
using LegalERP.Application.Notifications;
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
        var pushService = scope.ServiceProvider.GetRequiredService<IWebPushNotificationService>();

        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
        var tomorrowIso = tomorrow.ToString("yyyy-MM-dd");

        var hearings = await db.CaseHearings
            .Include(h => h.Case)
            .Where(h => !h.IsDeleted && h.HearingDate == tomorrow)
            .ToListAsync();

        _logger.LogInformation("Found {Count} hearings scheduled for tomorrow ({Date})", hearings.Count, tomorrow);

        var notificationsCreated = 0;
        var processedHearingsInBatch = new System.Collections.Generic.HashSet<Guid>();

        foreach (var hearing in hearings)
        {
            var caseName = hearing.Case?.Title ?? "قضية غير محددة";
            var caseNumber = hearing.Case?.CaseNumber ?? "—";
            var tag = $"<!--h:{hearing.Id}-->";

            // In-batch duplicate check per hearing
            if (processedHearingsInBatch.Contains(hearing.Id))
            {
                _logger.LogInformation("Reminder already scheduled in this batch for Hearing {HearingId}. Skipping.", hearing.Id);
                continue;
            }

            // Database idempotency: skip if a reminder for this specific hearing was already created
            var alreadyNotified = await db.Notifications.AnyAsync(n =>
                !n.IsDeleted && (
                    n.Message.Contains(tag) ||
                    (n.CaseId == hearing.CaseId &&
                     n.Message.Contains(tomorrowIso) &&
                     (!string.IsNullOrWhiteSpace(hearing.Purpose) && n.Message.Contains(hearing.Purpose)))
                ));

            if (alreadyNotified)
            {
                _logger.LogInformation("Reminder already exists for Hearing {HearingId} (Case {CaseId}) on {Date}. Skipping duplicate.", hearing.Id, hearing.CaseId, tomorrow);
                continue;
            }

            var title = $"📅 تذكير بجلسة غداً - رقم القضية: {caseNumber}";
            var message = $"موعد الجلسة: {tomorrowIso} | عنوان القضية: {caseName}";

            if (!string.IsNullOrWhiteSpace(hearing.Purpose))
            {
                message += $" | نوع الجلسة: {hearing.Purpose}";
            }

            // Append invisible hearing tracking tag for 100% deterministic per-hearing idempotency
            message += tag;

            // Save notification to database
            var notification = new Notification
            {
                Title = title,
                Message = message,
                CaseId = hearing.CaseId
            };

            await db.Notifications.AddAsync(notification);
            processedHearingsInBatch.Add(hearing.Id);
            notificationsCreated++;

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
        _logger.LogInformation("HearingReminderJob finished. {Count} notifications created.", notificationsCreated);
    }
}
