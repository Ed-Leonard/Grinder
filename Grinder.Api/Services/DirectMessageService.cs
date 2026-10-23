using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.DataAccess;
using Grinder.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Grinder.Api.Services;

/// <summary>
/// Interface for a service that handles direct messaging between users. Provides methods to send messages and retrieve conversation history.
/// </summary>
public interface IDirectMessageService
{
	Task<DirectMessageResponse> SendAsync(int senderId, int receiverId, string content);
	Task<List<DirectMessageResponse>?> GetConversationAsync(int userId, int otherUserId, int page, int limit);
	Task<List<ConversationPreviewResponse>> GetConversationPreviewsAsync(int userId);
	Task<List<DirectMessageResponse>> ShareTaskWithManyAsync(int senderId, IReadOnlyCollection<int> receiverIds, int taskId);
	Task<IReadOnlyList<FriendResponse>> GetShareableFriendsForTaskAsync(int userId, int taskId);
	Task MarkConversationReadAsync(int userId, int otherUserId);
}

/// <summary>
/// Handles persisting and retrieving direct messages between two users.
/// Used by ChatHub to save messages as they're sent, and by the frontend
/// to load conversation history when a conversation is opened.
/// </summary>
public class DirectMessageService : IDirectMessageService
{
	private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
	private readonly TimeProvider _timeProvider;
	private readonly ITaskService _taskService;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ILogger<DirectMessageService> _logger;

	public DirectMessageService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider, ITaskService taskService, INotificationService notificationService, IHubContext<ChatHub> chatHubContext, ILogger<DirectMessageService> logger)
	{
		_dbContextFactory = dbContextFactory;
		_timeProvider = timeProvider;
		_taskService = taskService;
        _notificationService = notificationService;
        _chatHubContext = chatHubContext;
        _logger = logger;
	}

	private const int PreviewMaxLength = 80;

	/// <summary>
	/// Persists a new direct message to the database and returns a DirectMessageResponse containing the saved message's details.
	/// </summary>
	/// <param name="senderId">The ID of the user sending the message.</param>
	/// <param name="receiverId">The ID of the user receiving the message.</param>
	/// <param name="content">The content of the message.</param>
	/// <returns>A task representing the asynchronous operation, with a DirectMessageResponse containing the saved message's details.</returns>
	public async Task<DirectMessageResponse> SendAsync(int senderId, int receiverId, string content)
	{
		if (string.IsNullOrWhiteSpace(content) || content.Length > 256)
		{
			throw new ArgumentException("Message must be between 1 and 256 characters");
		}

		await using var db = await _dbContextFactory.CreateDbContextAsync();

		if (!await AreMutualFriendsAsync(db, senderId, receiverId))
		{
			throw new InvalidOperationException("You can only message friends.");
		}
		var message = new DirectMessage
		{
			SenderId = senderId,
			ReceiverId = receiverId,
			Content = content,
			SentAt = _timeProvider.GetUtcNow()
		};
		db.DirectMessages.Add(message);
		await db.SaveChangesAsync();

		try
		{
			await _notificationService.CreateAsync(receiverId, senderId, NotificationType.Message, messageContent: content);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create notification of type {Type} from {SenderId} to {RecipientId}", NotificationType.Message, senderId, receiverId);
		}

		return Map(message);
	}

	/// <summary>
	/// Retrieves the paginated conversation between two users.
	/// Returns null if they are not mutual friends.
	/// </summary>
	public async Task<List<DirectMessageResponse>?> GetConversationAsync(
	int userId, int otherUserId, int page, int limit)
	{
		await using var db = await _dbContextFactory.CreateDbContextAsync();
		if (!await AreMutualFriendsAsync(db, userId, otherUserId))
		{
			return null;
		}

		page = Math.Max(page, 1);
		limit = Math.Clamp(limit, 1, 100);

		var rows = await db.DirectMessages.AsNoTracking()
	.Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId)
			|| (m.SenderId == otherUserId && m.ReceiverId == userId))
	.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id)
	.Skip((page - 1) * limit)
	.Take(limit)
	.ToListAsync();

		var taskIds = rows
			.Where(m => m.SharedTaskId.HasValue)
			.Select(m => m.SharedTaskId!.Value)
			.Distinct()
			.ToList();

		var previews = await _taskService.GetSharedTaskPreviewsAsync(userId, taskIds);

		var messages = rows
			.Select(m => Map(m, m.SharedTaskId is int taskId ? previews.GetValueOrDefault(taskId) : null))
			.ToList();

		messages.Reverse(); // back to chronological order for display

		if (page == 1)
		{
			await MarkConversationReadAsync(userId, otherUserId);
		}

		return messages;
	}

	/// <summary>
	/// Retrieves a list of conversation previews for the authenticated user, including the latest message and unread count.
	/// </summary>
	/// <param name="userId">The ID of the user</param>
	/// <returns>
	/// List of conversation previews for the authenticated user
	/// </returns>
	public async Task<List<ConversationPreviewResponse>> GetConversationPreviewsAsync(int userId)
	{
		await using var db = await _dbContextFactory.CreateDbContextAsync();

		var friendIds = await MutualFriendIds(db, userId).ToListAsync();
		if (friendIds.Count == 0)
		{
			return [];
		}

		var lastMessageIds = await db.DirectMessages
			.Where(m => m.SenderId == userId || m.ReceiverId == userId)
			.Where(m => friendIds.Contains(m.SenderId == userId ? m.ReceiverId : m.SenderId))
			.GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
			.Select(g => g.Max(m => m.Id))
			.ToListAsync();

		var lastMessages = await db.DirectMessages
			.AsNoTracking()
			.Where(m => lastMessageIds.Contains(m.Id))
			.Select(m => new
			{
				m.Id,
				m.SenderId,
				m.Content,
				m.SentAt,
				OtherUserId = m.SenderId == userId ? m.ReceiverId : m.SenderId,
				OtherUserDisplayName = m.SenderId == userId ? m.Receiver.DisplayName : m.Sender.DisplayName,
			})
			.ToListAsync();

		var unreadBySender = await db.DirectMessages
			.AsNoTracking()
			.Where(m => m.ReceiverId == userId && !m.IsRead)
			.GroupBy(m => m.SenderId)
			.Select(g => new { SenderId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.SenderId, x => x.Count);

		return lastMessages
			.OrderByDescending(m => m.SentAt)
			.ThenByDescending(m => m.Id)
			.Select(m => new ConversationPreviewResponse(
				m.OtherUserId,
				m.OtherUserDisplayName,
				TruncatePreview(m.Content ?? "Shared a task"),
				m.SenderId,
				m.SentAt,
				unreadBySender.GetValueOrDefault(m.OtherUserId)
				))
			.ToList();
	}

	/// <summary>
	/// Sets all messages in the conversation between two users as read
	/// </summary>
	/// <param name="userId">The ID of the user</param>
	/// <param name="otherUserId">The ID of the user who the conversation is with
	/// </param>
	public async Task MarkConversationReadAsync(int userId, int otherUserId)
	{
		await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Capture the boundary up front so a message sent concurrently (after this point) is neither
        // marked read here nor reported as read to the sender via the event below.
        var lastReadMessageId = await db.DirectMessages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
            .MaxAsync(m => (int?)m.Id);

        if (lastReadMessageId is null)
        {
            return;
        }

        var rowsAffected = await db.DirectMessages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead && m.Id <= lastReadMessageId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));

        if (rowsAffected > 0)
        {
            // Lets otherUserId know userId has read their messages up to lastReadMessageId, so their UI can show "Read"
            await _chatHubContext.Clients.User(otherUserId.ToString()).SendAsync("ConversationRead", userId, lastReadMessageId);

            // Viewing the conversation directly also clears userId's unread message notification from otherUserId.
            await _notificationService.MarkMessageNotificationsReadAsync(userId, otherUserId);
        }
    }

	/// <summary>
	/// Creates a direct message with the task preview and sends it to all users specified
	/// </summary>
	/// <param name="receiverIds">
	/// The IDs of the users the task is being sent to
	/// </param>
	/// <param name="taskId">
	/// The ID of the task being sent
	/// </param>
	public async Task<List<DirectMessageResponse>> ShareTaskWithManyAsync(
		int senderId, IReadOnlyCollection<int> receiverIds, int taskId)
	{
		await using var db = await _dbContextFactory.CreateDbContextAsync();

		var task = await FindTaskVisibleToAsync(db, senderId, taskId)
			?? throw new InvalidOperationException("Task not found.");

		// silently drop any requested id that isn't allowed to receive this task
		var eligibleIds = await EligibleRecipientIdsAsync(db, senderId, task);
		var validIds = eligibleIds.Where(receiverIds.Contains).ToList();

		var previews = await _taskService.GetSharedTaskPreviewsAsync(senderId, [taskId]);
		var preview = previews.GetValueOrDefault(taskId);

		var now = _timeProvider.GetUtcNow();
		var messages = validIds.Select(id => new DirectMessage
		{
			SenderId = senderId,
			ReceiverId = id,
			SharedTaskId = taskId,
			SentAt = now
		}).ToList();

		db.DirectMessages.AddRange(messages);
		await db.SaveChangesAsync();

		return messages.Select(m => Map(m, preview)).ToList();
	}

	/// <summary>
	/// Retrieves a list of friends that are eligible to be sent a given task
	/// </summary>
	/// <param name="userId">
	/// The ID of the user wanting to share the task
	/// </param>
	/// <param name="taskId">
	/// The ID of the task being sent
	/// </param>
	/// <returns>
	/// A list of friends who can view the task and have not already been sent it by the user
	/// </returns>
	public async Task<IReadOnlyList<FriendResponse>> GetShareableFriendsForTaskAsync(int userId, int taskId)
	{
		await using var db = await _dbContextFactory.CreateDbContextAsync();

		var task = await FindTaskVisibleToAsync(db, userId, taskId)
			?? throw new ArgumentException("Task not found.");

		var eligibleIds = await EligibleRecipientIdsAsync(db, userId, task);

		var alreadySharedWith = await db.DirectMessages.AsNoTracking()
			.Where(m => m.SenderId == userId && m.SharedTaskId == taskId)
			.Select(m => m.ReceiverId)
			.ToListAsync();

		var candidateIds = eligibleIds.Except(alreadySharedWith).ToList();

		return await db.Users.AsNoTracking()
			.Where(u => candidateIds.Contains(u.Id))
			.OrderBy(u => u.DisplayName)
			.Select(u => new FriendResponse(
				u.Id, u.DisplayName, u.ProfilePictureUrl, RelationshipStatus.Friends.ToString()))
			.ToListAsync();
	}

	private sealed record SharedTaskInfo(int OwnerId, TaskVisibility Visibility);

	/// <summary>
	/// Returns the task's owner and visibility, or null if it doesn't exist or the caller isn't allowed to see it.
	/// </summary>
	private static async Task<SharedTaskInfo?> FindTaskVisibleToAsync(DatabaseContext db, int userId, int taskId)
	{
		var task = await db.TaskItems.AsNoTracking()
			.Where(t => t.Id == taskId)
			.Select(t => new SharedTaskInfo(t.UserId, t.Visibility))
			.FirstOrDefaultAsync();

		if (task == null || task.OwnerId == userId)
			return task;

		if (task.Visibility == TaskVisibility.Private)
			return null;

		if (task.Visibility == TaskVisibility.FriendsOnly &&
			!await AreMutualFriendsAsync(db, userId, task.OwnerId))
			return null;

		return task;
	}

	/// <summary>
	/// The caller's mutual friends who are allowed to receive this task:
	/// everyone if it's the caller's own or public, or only friends shared with the owner if it's friends-only.
	/// </summary>
	private static async Task<List<int>> EligibleRecipientIdsAsync(
		DatabaseContext db, int userId, SharedTaskInfo task)
	{
		if (task.Visibility == TaskVisibility.Private) return [];
		var ids = MutualFriendIds(db, userId).Where(id => id != task.OwnerId);

		if (task.OwnerId != userId && task.Visibility == TaskVisibility.FriendsOnly)
		{
			var ownerFriendIds = await MutualFriendIds(db, task.OwnerId).ToListAsync();
			ids = ids.Where(id => ownerFriendIds.Contains(id));
		}

		return await ids.ToListAsync();
	}

	private static DirectMessageResponse Map(DirectMessage m, SharedTaskPreview? preview = null) =>
		new(m.Id, m.SenderId, m.ReceiverId, m.Content, m.SharedTaskId, preview, m.IsRead, m.SentAt);

	private static IQueryable<int> MutualFriendIds(DatabaseContext db, int userId) =>
		db.Follow.AsNoTracking()
			.Where(f => f.FollowerId == userId &&
						db.Follow.Any(r => r.FollowerId == f.FollowedId && r.FollowedId == userId))
			.Select(f => f.FollowedId);

	private static async Task<bool> AreMutualFriendsAsync(DatabaseContext db, int userId, int otherUserId)
	{
		if (userId == otherUserId)
		{
			return false;
		}

		return await db.Follow.AnyAsync(f => f.FollowerId == userId && f.FollowedId == otherUserId)
			&& await db.Follow.AnyAsync(f => f.FollowerId == otherUserId && f.FollowedId == userId);
	}

	private static string TruncatePreview(string content)
	{
		var trimmed = content.Replace('\n', ' ').Trim();
		return trimmed.Length <= PreviewMaxLength
			? trimmed
			: trimmed[..PreviewMaxLength].TrimEnd() + "...";
	}
}
