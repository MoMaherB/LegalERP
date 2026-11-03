using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using FluentAssertions;
using LegalERP.Application.Notifications;
using LegalERP.Web;
using LegalERP.Web.Components.Shared;
using LegalERP.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Notifications;

public class NotificationBellTests : TestContext
{
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeNotifHttpMessageHandler _httpHandler = new();

    public NotificationBellTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new NotificationApiClient(_mockHttpFactory.Object));

        // Setup JSInterop for pushNotifications check
        JSInterop.Setup<bool>("pushNotifications.isSupported").SetResult(false);
    }

    [Fact]
    public void NotificationBell_WhenUnreadCountIsZero_DoesNotRenderBadge()
    {
        _httpHandler.UnreadCount = 0;

        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("span.badge.bg-danger").Should().BeEmpty();
        });
    }

    [Fact]
    public void NotificationBell_WhenUnreadCountPositive_RendersUnreadBadge()
    {
        _httpHandler.UnreadCount = 4;

        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("span.badge.bg-danger");
            badge.TextContent.Trim().Should().Be("4");
        });
    }

    [Fact]
    public void NotificationBell_WhenUnreadCountGreaterThanNine_RendersNinePlus()
    {
        _httpHandler.UnreadCount = 15;

        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
        {
            var badge = cut.Find("span.badge.bg-danger");
            badge.TextContent.Trim().Should().Be("9+");
        });
    }

    [Fact]
    public async Task NotificationBell_WhenDropdownToggled_LoadsAndRendersNotifications()
    {
        var caseId = Guid.NewGuid();
        _httpHandler.UnreadCount = 1;
        _httpHandler.Notifications =
        [
            new NotificationDto(
                Guid.NewGuid(),
                "تذكير بجلسة",
                "لديك جلسة غداً",
                caseId,
                false,
                DateTime.UtcNow)
        ];

        var cut = RenderComponent<NotificationBell>();

        var bellButton = cut.Find("button.btn-link");
        await cut.InvokeAsync(() => bellButton.Click());

        cut.WaitForAssertion(() =>
        {
            var dropdown = cut.Find(".notification-dropdown");
            dropdown.TextContent.Should().Contain("تذكير بجلسة");
            dropdown.TextContent.Should().Contain("لديك جلسة غداً");
            dropdown.QuerySelectorAll("span.badge.bg-primary").Should().ContainSingle();
        });
    }

    [Fact]
    public async Task NotificationBell_WhenEmptyNotifications_RendersNoNotificationsYet()
    {
        _httpHandler.UnreadCount = 0;
        _httpHandler.Notifications = [];

        var cut = RenderComponent<NotificationBell>();

        var bellButton = cut.Find("button.btn-link");
        await cut.InvokeAsync(() => bellButton.Click());

        cut.WaitForAssertion(() =>
        {
            var dropdown = cut.Find(".notification-dropdown");
            dropdown.TextContent.Should().Contain("NoNotificationsYet");
        });
    }

    [Fact]
    public async Task NotificationBell_WhenNotificationClicked_MarksAsReadAndNavigatesToCase()
    {
        var notifId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        _httpHandler.UnreadCount = 1;
        _httpHandler.Notifications =
        [
            new NotificationDto(
                notifId,
                "تذكير بجلسة",
                "تفاصيل القضية",
                caseId,
                false,
                DateTime.UtcNow)
        ];

        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<NotificationBell>();

        var bellButton = cut.Find("button.btn-link");
        await cut.InvokeAsync(() => bellButton.Click());

        var notifItem = cut.WaitForElement(".notification-item");
        await cut.InvokeAsync(() => notifItem.Click());

        _httpHandler.LastMarkedReadId.Should().Be(notifId);
        nav.Uri.Should().EndWith($"/cases/{caseId}");
        cut.FindAll(".notification-dropdown").Should().BeEmpty("clicking a notification should close the dropdown");
    }

    [Fact]
    public async Task NotificationBell_WhenMarkAllReadClicked_CallsApiAndClearsUnreadBadge()
    {
        _httpHandler.UnreadCount = 2;
        _httpHandler.Notifications =
        [
            new NotificationDto(Guid.NewGuid(), "إشعار 1", "تفاصيل 1", null, false, DateTime.UtcNow),
            new NotificationDto(Guid.NewGuid(), "إشعار 2", "تفاصيل 2", null, false, DateTime.UtcNow)
        ];

        var cut = RenderComponent<NotificationBell>();

        var bellButton = cut.Find("button.btn-link");
        await cut.InvokeAsync(() => bellButton.Click());

        var markAllBtn = cut.WaitForElement("button.btn-outline-primary");
        await cut.InvokeAsync(() => markAllBtn.Click());

        _httpHandler.MarkAllAsReadCallCount.Should().Be(1);
        cut.FindAll("span.badge.bg-danger").Should().BeEmpty();
    }

    private sealed class FakeNotifHttpMessageHandler : HttpMessageHandler
    {
        public int UnreadCount { get; set; }
        public List<NotificationDto> Notifications { get; set; } = [];
        public Guid? LastMarkedReadId { get; private set; }
        public int MarkAllAsReadCallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/notifications/unread-count", StringComparison.OrdinalIgnoreCase))
            {
                var payload = new UnreadCountDto(UnreadCount);
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, payload));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/api/notifications", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Notifications));
            }

            if (request.Method == HttpMethod.Put && path.Contains("/api/notifications/") && path.EndsWith("/read", StringComparison.OrdinalIgnoreCase))
            {
                var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var idIndex = Array.IndexOf(parts, "notifications") + 1;
                if (idIndex > 0 && idIndex < parts.Length && Guid.TryParse(parts[idIndex], out var id))
                {
                    LastMarkedReadId = id;
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (request.Method == HttpMethod.Put && path.EndsWith("/api/notifications/read-all", StringComparison.OrdinalIgnoreCase))
            {
                MarkAllAsReadCallCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
            };
        }
    }
}
