using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Grinder.Api.Models.DTOs;

namespace Grinder.Api.Hubs;

public interface INotificationClient
{
    Task ReceiveNotification(NotificationDto notification);
    Task ReceiveUnreadCount(int count);
}

[Authorize]
public class NotificationHub : Hub<INotificationClient> { }
