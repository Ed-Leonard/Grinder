using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Services;

public interface INotificationService
{
    Task<NotificationPageDto> ListAsync(int userId, int page, int limit, NotificationType? type, bool unreadOnly);
    Task<int> UnreadCountAsync(int userId);
    Task MarkReadAsync(int userId, int? notificationId = null);
    Task MarkMessageNotificationsReadAsync(int recipientId, int senderId);
    Task DeleteAsync(int userId, int notificationId);
    Task CreateAsync(int recipientId, int senderId, NotificationType type, int? entityId = null, int? commentId = null, string? commentContent = null, ReactionItemDto? reaction = null, string? messageContent = null);
}

/// <summary>
///  Service responsible for managing notifications for users, including listing, marking as read, deleting, and creating notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public NotificationService(IDbContextFactory<DatabaseContext> dbContextFactory,
        IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _dbContextFactory = dbContextFactory;
        _hubContext = hubContext;
    }

    /// <summary>
    ///  Retrieves a paginated list of notifications for a specific user, with optional filtering by type and unread status.
    /// </summary>
    /// <param name="userId">The ID of the user whose notifications are being retrieved.</param>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="limit">The number of notifications per page.</param>
    /// <param name="type">Optional filter by notification type.</param>
    /// <param name="unreadOnly">If true, only unread notifications are returned.</param>
    /// <returns>A paginated list of notifications.</returns>
    public async Task<NotificationPageDto> ListAsync(int userId, int page, int limit, NotificationType? type, bool unreadOnly)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        limit = Math.Clamp(limit, 1, 100);
        page = Math.Max(page, 1);

        var query = context.Notification
            .AsNoTracking()
            .Where(n => n.RecipientId == userId);

        if (type is not null)
            query = query.Where(n => n.Type == type);

        if (unreadOnly)
            query = query.Where(n => !n.Read);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Include(n => n.Sender)
            .Include(n => n.Comment)
            .Include(n => n.Reaction)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                SenderId = n.SenderId,
                SenderName = n.Sender.DisplayName,
                Type = n.Type,
                EntityId = n.EntityId,
                Read = n.Read,
                CreatedAt = n.CreatedAt,
                CommentContent = n.Comment != null && n.Comment.IsDeleted == false ? n.Comment.Content : null,
                Reaction = n.Reaction != null
                    ? new ReactionItemDto(n.Reaction.Id, n.Reaction.Name, n.Reaction.FileName)
                    : null,
                MessageCount = n.MessageCount,
                MessagePreview = n.MessagePreview
            })
            .ToListAsync();

        return new NotificationPageDto
        {
            Items = items,
            Page = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)limit)
        };
    }

    /// <summary>
    ///  Retrieves the count of unread notifications for a specific user. Message notifications are
    ///  weighted by how many messages they represent, since repeated messages bump a single row.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>The count of unread notifications for the specified user.</returns>
    public async Task<int> UnreadCountAsync(int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Notification
            .AsNoTracking()
            .Where(n => n.RecipientId == userId && !n.Read)
            .SumAsync(n => n.Type == NotificationType.Message ? n.MessageCount : 1);
    }

    /// <summary>
    /// Marks a specific notification as read for a user, or marks all notifications as read if no specific notification ID is provided.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="notificationId"></param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task MarkReadAsync(int userId, int? notificationId = null)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        if (notificationId.HasValue)
        {
            var notification = await GetOwnedAsync(context, userId, notificationId.Value);

            if (!notification.Read)
            {
                notification.Read = true;
                await context.SaveChangesAsync();
            }
        }
        else
        {
            await context.Notification
                .Where(n => n.RecipientId == userId && !n.Read)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.Read, true));
        }
    }

    /// <summary>
    /// Marks the sender's unread message notification for a recipient as read, e.g. when the recipient
    /// opens the conversation directly rather than clicking the notification itself.
    /// </summary>
    public async Task MarkMessageNotificationsReadAsync(int recipientId, int senderId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var notification = await context.Notification.FirstOrDefaultAsync(n =>
            n.RecipientId == recipientId &&
            n.SenderId == senderId &&
            n.Type == NotificationType.Message &&
            !n.Read);

        if (notification is null)
            return;

        notification.Read = true;
        await context.SaveChangesAsync();

        var sender = await context.Users.FindAsync(senderId);
        await BroadcastAsync(recipientId, ToDto(notification, sender?.DisplayName ?? ""));
    }

    /// <summary>
    /// Deletes a specific notification for a user. If the notification does not exist or the user does not own it, an exception is thrown.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="notificationId"></param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteAsync(int userId, int notificationId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var notification = await GetOwnedAsync(context, userId, notificationId);
        var wasUnread = !notification.Read;

        context.Notification.Remove(notification);
        await context.SaveChangesAsync();

        if (wasUnread)
        {
            var recipient = _hubContext.Clients.User(userId.ToString());
            await recipient.ReceiveUnreadCount(await UnreadCountAsync(userId));
        }
    }

    /// <summary>
    ///  Creates a new notification for a user.
    /// </summary>
    /// <param name="recipientId">The ID of the user receiving the notification.</param>
    /// <param name="senderId">The ID of the user sending the notification.</param>
    /// <param name="type">The type of the notification.</param>
    /// <param name="entityId">Optional ID of the related entity.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CreateAsync(int recipientId, int senderId, NotificationType type, int? entityId = null, int? commentId = null, string? commentContent = null, ReactionItemDto? reaction = null, string? messageContent = null)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        if (recipientId == senderId)
            return;

        if (type == NotificationType.Follow)
        {
            var existing = await context.Notification.FirstOrDefaultAsync(n =>
                n.RecipientId == recipientId &&
                n.SenderId == senderId &&
                n.Type == NotificationType.Follow);

            if (existing is not null)
            {
                existing.CreatedAt = DateTime.UtcNow;
                existing.Read = false;
                await context.SaveChangesAsync();

                var followSender = await context.Users.FindAsync(senderId);
                await BroadcastAsync(recipientId, ToDto(existing, followSender?.DisplayName ?? ""));
                return;
            }
        }

        // Bump the existing unread message notification instead of spamming one per DM.
        if (type == NotificationType.Message)
        {
            var existingMessage = await context.Notification.FirstOrDefaultAsync(n =>
                n.RecipientId == recipientId &&
                n.SenderId == senderId &&
                n.Type == NotificationType.Message &&
                !n.Read);

            if (existingMessage is not null)
            {
                existingMessage.CreatedAt = DateTime.UtcNow;
                existingMessage.MessageCount += 1;
                existingMessage.MessagePreview = TruncateMessagePreview(messageContent);
                await context.SaveChangesAsync();

                var messageSender = await context.Users.FindAsync(senderId);
                await BroadcastAsync(recipientId, ToDto(existingMessage, messageSender?.DisplayName ?? ""));
                return;
            }
        }

        var notification = new Notification
        {
            RecipientId = recipientId,
            SenderId = senderId,
            Type = type,
            EntityId = entityId,
            Read = false,
            CreatedAt = DateTime.UtcNow,
            CommentId = type == NotificationType.Comment ? commentId : null,
            ReactionId = type == NotificationType.React ? reaction?.Id : null,
            MessageCount = 1,
            MessagePreview = type == NotificationType.Message ? TruncateMessagePreview(messageContent) : null,
        };

        context.Notification.Add(notification);

        await context.SaveChangesAsync();

        var sender = await context.Users.FindAsync(senderId);

        await BroadcastAsync(recipientId, ToDto(notification, sender?.DisplayName ?? "", commentContent, reaction));
    }

    private static NotificationDto ToDto(Notification n, string senderName, string? commentContent = null, ReactionItemDto? reaction = null) =>
        new()
        {
            Id = n.Id,
            SenderId = n.SenderId,
            SenderName = senderName,
            Type = n.Type,
            EntityId = n.EntityId,
            Read = n.Read,
            CreatedAt = n.CreatedAt,
            CommentContent = commentContent,
            Reaction = reaction,
            MessageCount = n.MessageCount,
            MessagePreview = n.MessagePreview
        };

    private async Task BroadcastAsync(int recipientId, NotificationDto dto)
    {
        var recipient = _hubContext.Clients.User(recipientId.ToString());
        await recipient.ReceiveNotification(dto);
        await recipient.ReceiveUnreadCount(await UnreadCountAsync(recipientId));
    }

    private const int MessagePreviewMaxLength = 80;

    private static string? TruncateMessagePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var trimmed = content.Replace('\n', ' ').Trim();
        return trimmed.Length <= MessagePreviewMaxLength
            ? trimmed
            : trimmed[..MessagePreviewMaxLength].TrimEnd() + "...";
    }

    /// <summary>
    ///  Retrieves a notification by ID and ensures that it belongs to the specified user. If the notification does not exist or does not belong to the user, an exception is thrown.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="userId">The ID of the user who should own the notification.</param>
    /// <param name="notificationId">The ID of the notification to retrieve.</param>
    /// <returns>The notification if it exists and belongs to the user.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    private async Task<Notification> GetOwnedAsync(DatabaseContext context, int userId, int notificationId)
    {
        var notification = await context.Notification.FirstOrDefaultAsync(n => n.Id == notificationId);

        if (notification is null)
            throw new KeyNotFoundException($"Notification {notificationId} not found.");

        if (notification.RecipientId != userId)
            throw new UnauthorizedAccessException("You do not have access to this notification.");

        return notification;
    }
}
