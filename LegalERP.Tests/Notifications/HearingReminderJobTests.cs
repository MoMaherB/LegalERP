using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Application.Notifications;
using LegalERP.Domain.Entities;
using LegalERP.Domain.Enums;
using LegalERP.Infrastructure.Persistence;
using LegalERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LegalERP.Tests.Notifications;

public class HearingReminderJobTests
{
    private readonly Mock<IWebPushNotificationService> _mockPushService = new();
    private readonly Mock<ILogger<HearingReminderJob>> _mockLogger = new();

    private DbContextOptions<ApplicationDbContext> CreateOptions(string dbName) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

    private HearingReminderJob CreateJob(string dbName)
    {
        var options = CreateOptions(dbName);

        var services = new ServiceCollection();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddScoped(_ => _mockPushService.Object);

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new HearingReminderJob(scopeFactory, _mockLogger.Object);
    }

    private ApplicationDbContext CreateDb(string dbName) => new(CreateOptions(dbName));

    [Fact]
    public async Task ExecuteAsync_WhenHearingIsTomorrow_CreatesArabicNotificationAndSendsPush()
    {
        var dbName = "HearingReminderTest_" + Guid.NewGuid();
        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

        var testCase = new Case
        {
            CaseNumber = "105/2026",
            Title = "شركة الأمل ضد بنك مصر",
            CaseType = CaseType.Civil,
            Status = CaseStatus.Active
        };

        await using (var seedDb = CreateDb(dbName))
        {
            await seedDb.Cases.AddAsync(testCase);
            var hearing = new CaseHearing
            {
                Case = testCase,
                CaseId = testCase.Id,
                HearingDate = tomorrow,
                Purpose = "تقديم مذكرات الدفاع والمستندات",
                IsDeleted = false
            };
            await seedDb.CaseHearings.AddAsync(hearing);
            await seedDb.SaveChangesAsync();
        }

        var job = CreateJob(dbName);
        await job.ExecuteAsync();

        await using (var verifyDb = CreateDb(dbName))
        {
            var notifications = await verifyDb.Notifications.ToListAsync();
            notifications.Should().HaveCount(1);
            var notif = notifications[0];

            notif.CaseId.Should().Be(testCase.Id);
            notif.Title.Should().Contain("105/2026");
            notif.Title.Should().Contain("رقم القضية");
            notif.Title.Should().Contain("تذكير بجلسة غداً");
            notif.Message.Should().Contain("عنوان القضية");
            notif.Message.Should().Contain("شركة الأمل ضد بنك مصر");
            notif.Message.Should().Contain("موعد الجلسة");
            notif.Message.Should().Contain(tomorrow.ToString("yyyy-MM-dd"));
            notif.Message.Should().Contain("نوع الجلسة");
            notif.Message.Should().Contain("تقديم مذكرات الدفاع والمستندات");
        }

        _mockPushService.Verify(p => p.SendToAllSubscribersAsync(
            It.Is<string>(t => t.Contains("105/2026")),
            It.Is<string>(m => m.Contains("شركة الأمل ضد بنك مصر")),
            testCase.Id), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCaseHasMultipleHearingsOnSameDay_CreatesNotificationForEachHearingAndRemainsIdempotent()
    {
        var dbName = "HearingReminderTest_Batch_" + Guid.NewGuid();
        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

        var testCase = new Case
        {
            CaseNumber = "999/2026",
            Title = "قضية متعددة الجلسات",
            CaseType = CaseType.Civil,
            Status = CaseStatus.Active
        };

        var hearing1 = new CaseHearing
        {
            Case = testCase,
            CaseId = testCase.Id,
            HearingDate = tomorrow,
            Purpose = "جلسة أولى",
            IsDeleted = false
        };
        var hearing2 = new CaseHearing
        {
            Case = testCase,
            CaseId = testCase.Id,
            HearingDate = tomorrow,
            Purpose = "جلسة مسائية",
            IsDeleted = false
        };

        await using (var seedDb = CreateDb(dbName))
        {
            await seedDb.Cases.AddAsync(testCase);
            await seedDb.CaseHearings.AddRangeAsync(hearing1, hearing2);
            await seedDb.SaveChangesAsync();
        }

        var job = CreateJob(dbName);
        await job.ExecuteAsync();

        await using (var verifyDb = CreateDb(dbName))
        {
            var notifications = await verifyDb.Notifications.Where(n => n.CaseId == testCase.Id).ToListAsync();
            notifications.Should().HaveCount(2, "each hearing should create its own reminder notification");
        }

        // Idempotency: second execution should not duplicate them
        await job.ExecuteAsync();

        await using (var verifyDb = CreateDb(dbName))
        {
            var notifications = await verifyDb.Notifications.Where(n => n.CaseId == testCase.Id).ToListAsync();
            notifications.Should().HaveCount(2, "subsequent executions must not duplicate reminders for the same hearings");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenExecutedTwiceOnSameDay_DoesNotCreateDuplicateNotificationOrResendPush()
    {
        var dbName = "HearingReminderTest_Idempotent_" + Guid.NewGuid();
        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

        var testCase = new Case
        {
            CaseNumber = "202/2026",
            Title = "نزاع تجاري",
            CaseType = CaseType.Civil,
            Status = CaseStatus.Active
        };

        await using (var seedDb = CreateDb(dbName))
        {
            await seedDb.Cases.AddAsync(testCase);
            var hearing = new CaseHearing
            {
                Case = testCase,
                CaseId = testCase.Id,
                HearingDate = tomorrow,
                Purpose = "حكم",
                IsDeleted = false
            };
            await seedDb.CaseHearings.AddAsync(hearing);
            await seedDb.SaveChangesAsync();
        }

        var job = CreateJob(dbName);

        // First execution
        await job.ExecuteAsync();

        // Second execution on same day
        await job.ExecuteAsync();

        await using (var verifyDb = CreateDb(dbName))
        {
            var notifications = await verifyDb.Notifications.Where(n => n.CaseId == testCase.Id).ToListAsync();
            notifications.Should().HaveCount(1, "running the job twice should be idempotent and not create duplicate notifications");
        }

        _mockPushService.Verify(p => p.SendToAllSubscribersAsync(
            It.IsAny<string>(), It.IsAny<string>(), testCase.Id), Times.Once, "push should only be sent once per hearing");
    }

    [Fact]
    public async Task ExecuteAsync_WhenHearingIsDeletedOrNotTomorrow_DoesNotCreateNotification()
    {
        var dbName = "HearingReminderTest_Skipped_" + Guid.NewGuid();
        var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));

        var testCase = new Case
        {
            CaseNumber = "303/2026",
            Title = "قضية عمالية",
            CaseType = CaseType.Civil,
            Status = CaseStatus.Active
        };

        await using (var seedDb = CreateDb(dbName))
        {
            await seedDb.Cases.AddAsync(testCase);

            // Hearing tomorrow but soft deleted
            await seedDb.CaseHearings.AddAsync(new CaseHearing
            {
                Case = testCase,
                HearingDate = tomorrow,
                IsDeleted = true
            });

            // Hearing next week
            await seedDb.CaseHearings.AddAsync(new CaseHearing
            {
                Case = testCase,
                HearingDate = tomorrow.AddDays(6),
                IsDeleted = false
            });

            await seedDb.SaveChangesAsync();
        }

        var job = CreateJob(dbName);
        await job.ExecuteAsync();

        await using (var verifyDb = CreateDb(dbName))
        {
            var notifications = await verifyDb.Notifications.ToListAsync();
            notifications.Should().BeEmpty();
        }

        _mockPushService.Verify(p => p.SendToAllSubscribersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
    }
}
