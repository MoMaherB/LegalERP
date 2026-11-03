using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Notifications;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace LegalERP.Tests.Notifications;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationRepository> _mockRepo = new();
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _controller = new NotificationsController(_mockRepo.Object);
    }

    [Fact]
    public void NotificationsController_ShouldHaveAuthorizeAttribute()
    {
        var authAttr = typeof(NotificationsController).GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull("NotificationsController must be secured with [Authorize] to prevent unauthenticated access");
    }

    [Fact]
    public async Task GetAll_ReturnsMappedNotificationDtos()
    {
        var caseId = Guid.NewGuid();
        var notifications = new List<Notification>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "جلسة غداً",
                Message = "تفاصيل الجلسة",
                CaseId = caseId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        var result = await _controller.GetAll(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var list = okResult.Value.Should().BeAssignableTo<List<NotificationDto>>().Subject;
        list.Should().HaveCount(1);
        list[0].Title.Should().Be("جلسة غداً");
        list[0].CaseId.Should().Be(caseId);
        list[0].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsUnreadCountDto()
    {
        _mockRepo.Setup(r => r.GetUnreadCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _controller.GetUnreadCount(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<UnreadCountDto>().Subject;
        dto.Count.Should().Be(5);
    }

    [Fact]
    public async Task MarkAsRead_WhenFound_MarksReadAndReturnsNoContent()
    {
        var notif = new Notification { Id = Guid.NewGuid(), IsRead = false };
        _mockRepo.Setup(r => r.GetByIdAsync(notif.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notif);

        var result = await _controller.MarkAsRead(notif.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.MarkAsRead(notif), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotFound_ReturnsNotFound()
    {
        var notifId = Guid.NewGuid();
        _mockRepo.Setup(r => r.GetByIdAsync(notifId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var result = await _controller.MarkAsRead(notifId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.MarkAsRead(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task MarkAllAsRead_CallsRepoAndReturnsNoContent()
    {
        var result = await _controller.MarkAllAsRead(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.MarkAllAsReadAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Subscribe_WhenNewEndpoint_AddsSubscriptionAndSaves()
    {
        var dto = new CreatePushSubscriptionDto("https://fcm.googleapis.com/test", "p256", "authKey", "Desktop Chrome");
        _mockRepo.Setup(r => r.GetSubscriptionByEndpointAsync(dto.Endpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PushSubscription?)null);

        var result = await _controller.Subscribe(dto, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        _mockRepo.Verify(r => r.AddSubscriptionAsync(It.Is<PushSubscription>(s => s.Endpoint == dto.Endpoint), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Subscribe_WhenEndpointAlreadyExists_ReturnsOkWithoutAddingDuplicate()
    {
        var dto = new CreatePushSubscriptionDto("https://fcm.googleapis.com/existing", "p256", "authKey", "Mobile");
        var existing = new PushSubscription { Endpoint = dto.Endpoint };
        _mockRepo.Setup(r => r.GetSubscriptionByEndpointAsync(dto.Endpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _controller.Subscribe(dto, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        _mockRepo.Verify(r => r.AddSubscriptionAsync(It.IsAny<PushSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unsubscribe_WhenFound_RemovesAndReturnsNoContent()
    {
        var endpoint = "https://fcm.googleapis.com/test";
        var existing = new PushSubscription { Endpoint = endpoint };
        _mockRepo.Setup(r => r.GetSubscriptionByEndpointAsync(endpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _controller.Unsubscribe(endpoint, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        _mockRepo.Verify(r => r.RemoveSubscription(existing), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unsubscribe_WhenNotFound_ReturnsNotFound()
    {
        var endpoint = "https://fcm.googleapis.com/notfound";
        _mockRepo.Setup(r => r.GetSubscriptionByEndpointAsync(endpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PushSubscription?)null);

        var result = await _controller.Unsubscribe(endpoint, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        _mockRepo.Verify(r => r.RemoveSubscription(It.IsAny<PushSubscription>()), Times.Never);
    }

    [Fact]
    public void TriggerReminders_ShouldHaveHttpPostAttributeWithCorrectTemplate()
    {
        var method = typeof(NotificationsController).GetMethod("TriggerReminders");
        method.Should().NotBeNull();
        var postAttr = method!.GetCustomAttribute<HttpPostAttribute>();
        postAttr.Should().NotBeNull();
        postAttr!.Template.Should().Be("trigger-reminders");
    }
}
