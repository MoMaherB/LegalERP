using LegalERP.Application.Notifications;
using LegalERP.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace LegalERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repository;

    public NotificationsController(INotificationRepository repository) => _repository = repository;

    // GET /api/notifications
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetAll(CancellationToken ct)
    {
        var notifications = await _repository.GetAllAsync(ct);
        return Ok(notifications.Select(ToDto).ToList());
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken ct)
    {
        var count = await _repository.GetUnreadCountAsync(ct);
        return Ok(new UnreadCountDto(count));
    }

    // PUT /api/notifications/{id}/read
    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var notification = await _repository.GetByIdAsync(id, ct);
        if (notification is null) return NotFound();

        _repository.MarkAsRead(notification);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<ActionResult> MarkAllAsRead(CancellationToken ct)
    {
        await _repository.MarkAllAsReadAsync(ct);
        return NoContent();
    }

    // POST /api/notifications/subscribe
    [HttpPost("subscribe")]
    public async Task<ActionResult> Subscribe(CreatePushSubscriptionDto dto, CancellationToken ct)
    {
        // Check if subscription already exists
        var existing = await _repository.GetSubscriptionByEndpointAsync(dto.Endpoint, ct);
        if (existing != null)
        {
            return Ok(); // Already subscribed
        }

        var subscription = new PushSubscription
        {
            Endpoint = dto.Endpoint,
            P256dh = dto.P256dh,
            Auth = dto.Auth,
            DeviceName = dto.DeviceName
        };

        await _repository.AddSubscriptionAsync(subscription, ct);
        await _repository.SaveChangesAsync(ct);

        return Ok();
    }

    // POST /api/notifications/unsubscribe
    [HttpPost("unsubscribe")]
    public async Task<ActionResult> Unsubscribe([FromBody] string endpoint, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByEndpointAsync(endpoint, ct);
        if (subscription is null) return NotFound();

        _repository.RemoveSubscription(subscription);
        await _repository.SaveChangesAsync(ct);

        return NoContent();
    }

    // GET /api/notifications/vapid-public-key
    [HttpGet("vapid-public-key")]
    public ActionResult<string> GetVapidPublicKey([FromServices] IConfiguration config)
    {
        var publicKey = config["VapidKeys:PublicKey"];
        if (string.IsNullOrEmpty(publicKey))
            return NotFound("VAPID public key not configured.");

        return Ok(publicKey);
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id,
        n.Title,
        n.Message,
        n.CaseId,
        n.IsRead,
        n.CreatedAt
    );
}
