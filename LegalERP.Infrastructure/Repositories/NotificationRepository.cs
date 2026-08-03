using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalERP.Application.Notifications;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LegalERP.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _db;

    public NotificationRepository(ApplicationDbContext db) => _db = db;

    // --- Notifications ---

    public async Task<List<Notification>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => !n.IsRead, ct);

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default) =>
        await _db.Notifications.AddAsync(notification, ct);

    public void MarkAsRead(Notification notification)
    {
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        notification.UpdatedAt = DateTime.UtcNow;
        _db.Notifications.Update(notification);
    }

    public async Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        await _db.Notifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow)
                .SetProperty(n => n.UpdatedAt, DateTime.UtcNow), ct);
    }

    // --- Push Subscriptions ---

    public async Task<List<PushSubscription>> GetAllSubscriptionsAsync(CancellationToken ct = default) =>
        await _db.PushSubscriptions.ToListAsync(ct);

    public async Task<PushSubscription?> GetSubscriptionByEndpointAsync(string endpoint, CancellationToken ct = default) =>
        await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);

    public async Task AddSubscriptionAsync(PushSubscription subscription, CancellationToken ct = default) =>
        await _db.PushSubscriptions.AddAsync(subscription, ct);

    public void RemoveSubscription(PushSubscription subscription)
    {
        _db.PushSubscriptions.Remove(subscription);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);
}
