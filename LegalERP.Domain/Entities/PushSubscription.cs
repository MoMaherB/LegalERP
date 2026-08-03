using System;
using LegalERP.Domain.Common;

namespace LegalERP.Domain.Entities;

public class PushSubscription : BaseEntity
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
}
