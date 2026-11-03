using System;
using System.Threading.Tasks;

namespace LegalERP.Application.Notifications;

public interface IWebPushNotificationService
{
    Task SendToAllSubscribersAsync(string title, string message, Guid? caseId = null);
}
