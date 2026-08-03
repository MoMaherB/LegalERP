using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Domain.Entities;

namespace LegalERP.Application.Notifications;

public interface INotificationRepository
{
    // Notifications
    Task<List<Notification>> GetAllAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
    void MarkAsRead(Notification notification);
    Task MarkAllAsReadAsync(CancellationToken ct = default);

    // Push Subscriptions
    Task<List<PushSubscription>> GetAllSubscriptionsAsync(CancellationToken ct = default);
    Task<PushSubscription?> GetSubscriptionByEndpointAsync(string endpoint, CancellationToken ct = default);
    Task AddSubscriptionAsync(PushSubscription subscription, CancellationToken ct = default);
    void RemoveSubscription(PushSubscription subscription);

    Task SaveChangesAsync(CancellationToken ct = default);
}
