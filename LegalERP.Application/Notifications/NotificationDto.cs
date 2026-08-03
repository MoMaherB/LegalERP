using System;

namespace LegalERP.Application.Notifications;

public record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    Guid? CaseId,
    bool IsRead,
    DateTime CreatedAt
);

public record CreatePushSubscriptionDto(
    string Endpoint,
    string P256dh,
    string Auth,
    string? DeviceName
);

public record UnreadCountDto(int Count);
