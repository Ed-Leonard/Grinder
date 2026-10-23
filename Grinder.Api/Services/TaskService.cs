using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Grinder.Api.Models.DTOs;

namespace Grinder.Api.Services;

/// <summary>
/// Provides business-logic operations for creating and retrieving tasks.
/// </summary>
public interface ITaskService
{
	Task<TaskItem> CreateNewTaskAsync(int userId, CreateTaskRequest request);

	Task<TaskItem?> PatchTaskAsync(int userId, int id, PatchTaskRequest patchRequest);
	Task<IEnumerable<TaskItem>> GetAllTasksAsync(int userId, int? requestedUserId);
	Task<TaskItem?> GetTaskByIdAsync(int userId, int id);
	Task ReorderTasks(List<ReorderTasksRequest> items, int userId);
	Task<IEnumerable<ValidParentTask>> GetAllValidParentTasksAsync(int userId);
	Task<bool> DeleteTaskAsync(int userId, int id);
	Task<StatusCountResponse> GetTaskStatusCountsAsync(int requestingUserId, int requestedUserId);
	Task<Dictionary<int, SharedTaskPreview>> GetSharedTaskPreviewsAsync(int viewerId, IReadOnlyCollection<int> taskIds);
}

/// <summary>
/// Implements task-related business logic.
/// </summary>
public class TaskService : ITaskService
{
	private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
	private readonly TimeProvider _timeProvider;
	private readonly INotificationService _notificationService;
	private readonly IFollowService _followService;
	private readonly ILogger<TaskService> _logger;
	private readonly IBadgeService _badgeService;

	public TaskService(
		IDbContextFactory<DatabaseContext> dbContextFactory,
		TimeProvider timeProvider,
		INotificationService notificationService,
		ILogger<TaskService> logger,
		IFollowService followService,
		IBadgeService badgeService
		)
	{
		_dbContextFactory = dbContextFactory;
		_timeProvider = timeProvider;
		_notificationService = notificationService;
		_followService = followService;
		_logger = logger;
		_badgeService = badgeService;
	}

	internal static void ValidateStartDateTime(DateOnly? startDate, TimeOnly? startTime, DateOnly? dueDate, TimeOnly? dueTime, TimeProvider timeProvider)
	{
		if (startTime.HasValue && !startDate.HasValue)
		{
			throw new ArgumentException("Invalid start time, time must be in the future", nameof(startTime));
		}
		var effectiveStartTime = startTime ?? new TimeOnly(0, 0);
		var effectiveDueTime = dueTime ?? new TimeOnly(23, 59);
		if (startDate.HasValue && dueDate.HasValue)
		{
			if (startDate.Value > dueDate.Value || (startDate.Value == dueDate.Value && effectiveStartTime >= effectiveDueTime))
			{
				throw new ArgumentException("Invalid start date and time, must be before due date and time", nameof(startDate));
			}
		}

	}


	internal static void ValidateDueDateTime(DateOnly? dueDate, TimeOnly? dueTime, TimeProvider timeProvider)
	{
		if (!dueDate.HasValue)
		{
			throw new ArgumentException("Due date is required", nameof(dueDate));
		}

		// due date must not be in the past
		var due = dueDate.Value.ToDateTime(dueTime ?? new TimeOnly(23, 59));
		var now = timeProvider.GetLocalNow().DateTime;
		if (dueTime.HasValue ? due < now : due.Date < now.Date)
		{
			throw new ArgumentException("Invalid due date, date must be in the future", nameof(dueDate));
		}
	}

	private static DateOnly? ParseOptionalDate(string? value, DateOnly? current, string paramName)
	{
		if (value == null) return current;
		if (value == "") return null;
		if (!DateOnly.TryParse(value, out var parsed))
			throw new ArgumentException("Invalid date format, must be in the format YYYY-MM-DD", paramName);
		return parsed;
	}

	private static TimeOnly? ParseOptionalTime(string? value, TimeOnly? current, string paramName)
	{
		if (value == null) return current;
		if (value == "") return null;
		if (!TimeOnly.TryParse(value, out var parsed))
			throw new ArgumentException("Invalid time format, must be in the format HH:MM", paramName);
		return parsed;
	}

	private static void UpdateClipStatus(TaskItem task, DateTimeOffset now)
	{
		bool wasClip = task.IsClip;
		task.IsClip = task.TaskAttachments.Count == 1 && task.TaskAttachments[0].ContentType.StartsWith("video/");

		if (task.IsClip && !wasClip)
		{
			task.ClipCreatedAt = now;
		}
		else if (!task.IsClip)
		{
			task.ClipCreatedAt = null;
		}
	}

	private static void UpdateCompletedAt(TaskItem task, DateTimeOffset now)
	{
		if (task.ItemStatus == TaskItemStatus.Done)
		{
			task.CompletedAt ??= now;
		}
		else
		{
			task.CompletedAt = null;
		}
	}

	/// <summary>
	/// Creates a new task within an optionally given parent task.
	/// </summary>
	/// <param name="parentTaskId">The ID of the tasks parent.</param>
	/// <param name="title">The task title.</param>
	/// <param name="description">Optional task description.</param>
	/// <param name="StartDate">Optional start date for the task.</param>
	/// <param name="StartTime">Optional start time for the task.</param>
	/// <param name="dueDate">Optional due date for the task. If provided, it must not be in the past.</param>
	/// <param name="dueTime">Optional due time for the task. If provided, it must not be in the past.</param>
	/// <param name="status">The initial status of the task.</param>
	/// <returns>The newly created Task Item.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="dueDate"/> or <paramref name="dueTime"/> is in the past.</exception>
	/// <remarks>
	/// Contract:
	/// Preconditions:
	/// - If dueDate or dueTime is provided, they must not be earlier than the current time.
	///
	/// Postconditions:
	/// - A TaskItem is persisted in the database.
	/// - The returned TaskItem has a non-zero Id.
	/// - CreatedAt is set using the injected TimeProvider.
	/// </remarks>
	public async Task<TaskItem> CreateNewTaskAsync(int userId, CreateTaskRequest request)
	{
		await using var context = await _dbContextFactory.CreateDbContextAsync();

		TaskItem? parent = null;
		int depth = 0;

		if (request.ParentTaskId != null)
		{
			parent = await context.TaskItems
				.FirstOrDefaultAsync(t => t.Id == request.ParentTaskId && t.UserId == userId);

			if (parent == null)
			{
				throw new ArgumentException("Parent task not found");
			}
			if (parent.Depth >= 2)
			{
				throw new InvalidOperationException("Cannot nest task beyond max depth");
			}
			depth = parent.Depth + 1;
		}

		ValidateStartDateTime(request.StartDate, request.StartTime, request.DueDate, request.DueTime, _timeProvider);
		ValidateDueDateTime(request.DueDate, request.DueTime, _timeProvider);
		if (!Enum.IsDefined(request.Visibility))
		{
			throw new ArgumentException(
				"Visibility must be Public, FriendsOnly, or Private",
				nameof(request.Visibility));
		}


		var categories = new List<Category>();
		if (request.CategoryIds is { Count: > 0 })
		{
			categories = await context.Category
				.Where(c => request.CategoryIds.Contains(c.Id))
				.ToListAsync();

			if (categories.Count != request.CategoryIds.Distinct().Count())
			{
				throw new ArgumentException("One or more categories not found", nameof(request.CategoryIds));
			}
		}

		var createdAt = _timeProvider.GetUtcNow();

		var taskItem = new TaskItem
		{
			UserId = userId,
			ParentTaskId = request.ParentTaskId,
			Depth = depth,
			Title = request.Title,
			Description = request.Description,
			StartDate = request.StartDate,
			StartTime = request.StartDate.HasValue ? request.StartTime ?? new TimeOnly(0, 0) : request.StartTime,
			DueDate = request.DueDate,
			DueTime = request.DueTime ?? new TimeOnly(23, 59),
			ItemStatus = request.ItemStatus,
			Visibility = request.Visibility,
			CreatedAt = createdAt,
			UpdatedAt = createdAt.UtcDateTime,
			Order = await context.TaskItems.CountAsync(t => t.ParentTaskId == request.ParentTaskId),
			Categories = categories,
		};
		UpdateCompletedAt(taskItem, createdAt);

		// Prevents a task from being created if supplied attachment IDs aren't valid
		await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

		try
		{
			context.Add(taskItem);

			// The first save generates the task ID
			await context.SaveChangesAsync();

			await AttachPendingAttachmentsAsync(context, userId, taskItem, request.AttachmentIds);
			await context.SaveChangesAsync();

			UpdateClipStatus(taskItem, createdAt);

			await context.SaveChangesAsync();
			await transaction.CommitAsync();
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}

		await _badgeService.EvaluateBadgesAsync(userId);
		return taskItem;
	}

	/// <summary>
	/// Retrieves all tasks in a tree structure.
	/// </summary>
	/// <returns>A collection of all Task Items records.</returns>
	/// <remarks>
	/// Postconditions:
	/// - Returns an empty collection if no tasks exist.
	/// - Never returns null.
	/// </remarks>
	public async Task<IEnumerable<TaskItem>> GetAllTasksAsync(int userId, int? requestedUserId)
	{
		var targetUserId = requestedUserId ?? userId;

		await using var context = await _dbContextFactory.CreateDbContextAsync();

		IQueryable<TaskItem> query = context.TaskItems
			.Include(t => t.TaskAttachments)
			.Include(t => t.Categories)
			.Include(t => t.Reactions).ThenInclude(r => r.Reaction)
			.Include(t => t.Children.OrderBy(c => c.Order))
				.ThenInclude(c => c.Categories)
			.Include(t => t.Children.OrderBy(c => c.Order))
				.ThenInclude(c => c.Children.OrderBy(g => g.Order))
					.ThenInclude(g => g.Categories)


			.Where(t => t.UserId == targetUserId && t.ParentTaskId == null);

		if (targetUserId != userId)
		{
			var relationshipStatuses = await _followService.GetRelationshipStatusesAsync(
				userId, new[] { targetUserId });

			var isFriend = relationshipStatuses.TryGetValue(targetUserId, out var status)
				&& status == RelationshipStatus.Friends;

			query = isFriend
				? query.Where(t => t.Visibility == TaskVisibility.Public || t.Visibility == TaskVisibility.FriendsOnly)
				: query.Where(t => t.Visibility == TaskVisibility.Public);
		}

		return await query
			.OrderBy(t => t.DueDate)
				.ThenBy(t => t.DueTime)
			.AsSplitQuery()
			.ToListAsync();
	}

	/// <summary>
	/// Retrieves all tasks that have a depth of 0 or 1 and hence are valid parent tasks.
	/// </summary>
	/// <returns>A collection of Id, Name DTOs</returns>
	/// <remarks>
	/// Postconditions:
	/// - Returns an empty collection if no tasks exist.
	/// - Never returns null.
	/// </remarks>
	public async Task<IEnumerable<ValidParentTask>> GetAllValidParentTasksAsync(int userId)
	{
		await using var context = await _dbContextFactory.CreateDbContextAsync();
		return await context.TaskItems
			.Include(t => t.TaskAttachments)
			.Where(t => t.UserId == userId && t.Depth <= 1)
			.OrderBy(t => t.DueDate)
				.ThenBy(t => t.DueTime)
			.Select(t => new ValidParentTask
			{
				Id = t.Id,
				Title = t.Title
			})
			.ToListAsync();
	}

	/// <summary>
	/// Retrieves a task by its ID.
	/// </summary>
	/// <param name="id">The task ID.</param>
	/// <returns>The matching TaskItem if found; otherwise null.</returns>
	public async Task<TaskItem?> GetTaskByIdAsync(int userId, int taskId)
	{
		await using var context = await _dbContextFactory.CreateDbContextAsync();
		var task = await context.TaskItems
			.Include(t => t.TaskAttachments)
			.Include(t => t.Categories)
			.Include(t => t.ParentTask)
			.Include(t => t.Children)
			.Include(t => t.Reactions).ThenInclude(r => r.Reaction)
			.FirstOrDefaultAsync(t => t.Id == taskId);

		if (task == null)
		{
			return null;
		}

		if (task.UserId == userId)
		{
			return task;
		}

		var relationshipStatuses = await _followService.GetRelationshipStatusesAsync(
			userId, new[] { task.UserId });

		var isFriend = relationshipStatuses.TryGetValue(task.UserId, out var status)
			&& status == RelationshipStatus.Friends;

		var isVisible = task.Visibility == TaskVisibility.Public
			|| (isFriend && task.Visibility == TaskVisibility.FriendsOnly);

		return isVisible ? task : null;
	}

	/// <summary>
	/// Reorders the tasks within a list by there new order values and updates their parent id if changed while preventing forming cycles
	/// </summary>
	/// <param name="items">All the tasks in the list</param>
	public async Task ReorderTasks(List<ReorderTasksRequest> items, int userId)
	{
		// claude helped here basically we need to prevent cycles from forming which is difficult
		await using var context = await _dbContextFactory.CreateDbContextAsync();

		var ids = items.Select(i => i.Id).ToList();
		var tasks = await context.TaskItems
			.Where(t => ids.Contains(t.Id) && t.UserId == userId)
			.ToListAsync();

		var parentIds = items
			.Where(i => i.ParentId != null)
			.Select(i => i.ParentId!.Value)
			.Distinct()
			.ToList();

		var validParents = await context.TaskItems
			.Where(t => parentIds.Contains(t.Id)
					 && t.UserId == userId
					 && t.Depth < 2)
			.ToDictionaryAsync(t => t.Id);

		// Build the proposed parent-id map for every task in this batch,
		// falling back to each task's existing parent if it's not being reparented.
		var proposedParent = tasks.ToDictionary(
			t => t.Id,
			t => items.First(i => i.Id == t.Id).ParentId ?? t.ParentTaskId);

		// Validate: walk each task's proposed ancestor chain and check for cycles/self-parenting.
		foreach (var task in tasks)
		{
			if (proposedParent.TryGetValue(task.Id, out var directParent) && directParent == task.Id)
			{
				throw new ArgumentException($"Task {task.Id} cannot be its own parent");
			}

			var visited = new HashSet<int> { task.Id };
			int? currentParentId = proposedParent.GetValueOrDefault(task.Id);

			while (currentParentId.HasValue)
			{
				if (!visited.Add(currentParentId.Value))
				{
					throw new ArgumentException($"Reorder would create a parent cycle involving task {task.Id}");
				}

				// Walk via the proposed map if the ancestor is also in this batch,
				// otherwise fall back to its real, unchanged parent.
				currentParentId = proposedParent.TryGetValue(currentParentId.Value, out var next)
					? next
					: tasks.FirstOrDefault(t => t.Id == currentParentId)?.ParentTaskId
					  ?? validParents.GetValueOrDefault(currentParentId.Value)?.ParentTaskId;
			}
		}

		// Only after validation passes do we actually mutate tracked entities.
		foreach (var item in items)
		{
			var task = tasks.FirstOrDefault(t => t.Id == item.Id);
			if (task == null) continue;

			if (item.ParentId.HasValue)
			{
				if (!validParents.TryGetValue(item.ParentId.Value, out var parent))
					continue;

				task.ParentTaskId = parent.Id;
				task.Depth = parent.Depth + 1;
			}

			task.Order = item.Order;
		}

		await context.SaveChangesAsync();
	}

	/// <summary>
	/// Edits an existing task.
	/// </summary>
	/// <param name="userId">The ID of the user making the edit request.</param>
	/// <param name="id">The ID of the task to edit.</param>
	/// <param name="request">The fields to update and their new values.</param>
	/// <returns>The updated task if successful; otherwise null.</returns>
	/// <remarks>
	/// Preconditions:
	/// - The task with the given ID must exist and belong to the user.
	/// - If dueDate or dueTime is being changed, it must not be in the past.
	/// - If dueTime is being changed, dueDate must also be provided and not in the past.
	/// - Description, dueDate and dueTime can be cleared by being set to null but Title and Status cannot be cleared; if dueTime is cleared, dueDate is also cleared.
	/// Postconditions:
	/// - If the task does not exist or does not belong to the user, null is returned.
	/// - If any provided values are invalid, an ArgumentException is thrown and no changes are made to the database.
	/// </remarks>
	public async Task<TaskItem?> PatchTaskAsync(int userId, int id, PatchTaskRequest request)
	{
		await using var context = await _dbContextFactory.CreateDbContextAsync();
		var task = await context.TaskItems
			.Include(t => t.TaskAttachments)
			.Include(t => t.Categories)
			.Include(t => t.Reactions).ThenInclude(r => r.Reaction)
			.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
		if (task == null)
		{
			return null;
		}

		await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

		try
		{
			var changedFields = new List<TaskField>();

			if (request.Title != null)
			{
				string title = request.Title.Trim();
				if (title.Length < 3 || title.Length > 128)
				{
					throw new ArgumentException("Title is required and must be between 3 and 128 characters long", nameof(request.Title));
				}
				if (task.Title != title) changedFields.Add(TaskField.Title);
				task.Title = title;
			}
			if (request.Description != null)
			{
				if (request.Description.Length > 2048)
				{
					throw new ArgumentException("Description must be 2048 characters or less", nameof(request.Description));
				}

				string newDescription = string.IsNullOrWhiteSpace(request.Description)
					? ""
					: request.Description;
				if (task.Description != newDescription) changedFields.Add(TaskField.Description);
				task.Description = newDescription;
			}

			if (request.StartDate != null || request.StartTime != null)
			{
				DateOnly? newStartDate = ParseOptionalDate(request.StartDate, task.StartDate, nameof(request.StartDate));
				TimeOnly? newStartTime = ParseOptionalTime(request.StartTime, task.StartTime, nameof(request.StartTime));

				if (request.StartDate == "")
				{
					newStartTime = null;
				}
				else if (newStartDate.HasValue && newStartTime == null)
				{
					newStartTime = new TimeOnly(0, 0);
				}

				DateOnly? effectiveDueDate = ParseOptionalDate(request.DueDate, task.DueDate, nameof(request.DueDate));
				TimeOnly? effectiveDueTime = ParseOptionalTime(request.DueTime, task.DueTime, nameof(request.DueTime))
					?? (effectiveDueDate.HasValue ? new TimeOnly(23, 59) : null);

				ValidateStartDateTime(newStartDate, newStartTime, effectiveDueDate, effectiveDueTime, _timeProvider);
				if (task.StartDate != newStartDate || task.StartTime != newStartTime) changedFields.Add(TaskField.Start);
				task.StartDate = newStartDate;
				task.StartTime = newStartTime;
			}

			if (request.DueDate != null || request.DueTime != null)
			{
				DateOnly? newDueDate = ParseOptionalDate(request.DueDate, task.DueDate, nameof(request.DueDate));
				if (!newDueDate.HasValue)
				{
					throw new ArgumentException("Due date is required", nameof(request.DueDate));
				}

				TimeOnly? newDueTime = ParseOptionalTime(request.DueTime, task.DueTime, nameof(request.DueTime))
					?? new TimeOnly(23, 59);

				ValidateDueDateTime(newDueDate, newDueTime, _timeProvider);
				if (task.DueDate != newDueDate || task.DueTime != newDueTime) changedFields.Add(TaskField.Due);
				task.DueDate = (DateOnly)newDueDate;
				task.DueTime = (TimeOnly)newDueTime;
				ValidateStartDateTime(task.StartDate, task.StartTime, newDueDate, newDueTime, _timeProvider);
			}
			if (request.ItemStatus != null)
			{
				if (!request.ItemStatus.HasValue)
				{
					throw new ArgumentException("Existing tasks must have a valid status", nameof(request.ItemStatus));
				}
				if (task.ItemStatus != request.ItemStatus.Value) changedFields.Add(TaskField.Status);
				task.ItemStatus = request.ItemStatus.Value;
				UpdateCompletedAt(task, _timeProvider.GetUtcNow());
			}

			if (request.Visibility.HasValue)
			{
				if (!Enum.IsDefined(request.Visibility.Value))
				{
					throw new ArgumentException(
						"Visibility must be Public, FriendsOnly, or Private",
						nameof(request.Visibility));
				}

				task.Visibility = request.Visibility.Value;
			}

			if (request.CategoryIds != null)
			{
				List<int> distinctCategoryIds = request.CategoryIds.Distinct().ToList();
				List<Category> categories = new();

				if (distinctCategoryIds.Count > 0)
				{
					categories = await context.Category
						.Where(c => distinctCategoryIds.Contains(c.Id))
						.ToListAsync();

					if (categories.Count != distinctCategoryIds.Count)
					{
						throw new ArgumentException("One or more categories not found", nameof(request.CategoryIds));
					}
				}

				task.Categories.Clear();
				foreach (Category category in categories)
				{
					task.Categories.Add(category);
				}
				changedFields.Add(TaskField.Categories);
			}

			await AttachPendingAttachmentsAsync(
				context,
				userId,
				task,
				request.AttachmentIds
			);
			UpdateClipStatus(task, _timeProvider.GetUtcNow());
			if (request.ParentTaskId != null)
			{
				if (request.ParentTaskId == -1)
				{
					task.ParentTaskId = null;
					task.Depth = 0;
					changedFields.Add(TaskField.ParentTask);
				}
				else
				{
					var parent = await context.TaskItems
						.FirstOrDefaultAsync(t => t.Id == request.ParentTaskId.Value && t.UserId == userId);

					if (parent == null)
					{
						throw new ArgumentException("Parent task not found", nameof(request.ParentTaskId));
					}
					if (parent.Depth >= 2)
					{
						throw new ArgumentException("Parent task is nested too deeply", nameof(request.ParentTaskId));
					}

					var current = parent;
					while (current != null)
					{
						if (current.Id == id)
						{
							throw new ArgumentException("Cannot set parent: would create a cycle", nameof(request.ParentTaskId));
						}
						current = current.ParentTaskId.HasValue
							? await context.TaskItems.FirstOrDefaultAsync(t => t.Id == current.ParentTaskId.Value && t.UserId == userId)
							: null;
					}

					task.ParentTaskId = parent!.Id;
					task.Depth = parent.Depth + 1;
					changedFields.Add(TaskField.ParentTask);
				}
			}


			if (changedFields.Count > 0)
			{
				task.UpdatedAt = _timeProvider.GetUtcNow();
				task.ChangedFields = changedFields.Distinct().ToList();
			}

			await context.SaveChangesAsync();
			await transaction.CommitAsync();
			await _badgeService.EvaluateBadgesAsync(userId);
			return task;
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}

	/// <summary>
	/// Attaches pending attachments to a task, ensuring they belong to the user and are not already attached to another task. 
	/// Validates that the total number of attachments does not exceed 5.
	/// </summary>
	/// <param name="context">The database context.</param>
	/// <param name="userId">The ID of the user.</param>
	/// <param name="task">The task to which attachments will be added.</param>
	/// <param name="attachmentIds">The IDs of the attachments to attach.</param>
	/// <exception cref="ArgumentException">Thrown when any attachment ID is invalid, already attached, or if the total number of attachments exceeds 5.</exception>
	private static async Task AttachPendingAttachmentsAsync(DatabaseContext context, int userId, TaskItem task,
		IReadOnlyCollection<int>? attachmentIds)
	{
		if (attachmentIds == null || attachmentIds.Count == 0)
		{
			return;
		}

		List<int> uniqueIds = attachmentIds.Distinct().ToList();

		if (uniqueIds.Count != attachmentIds.Count)
		{
			throw new ArgumentException("Attachment IDs must be unique", nameof(attachmentIds));
		}

		int existingAttachmentCount = await context.TaskAttachments.CountAsync(attachment => attachment.TaskItemId == task.Id);

		if (existingAttachmentCount + uniqueIds.Count > 5)
		{
			throw new ArgumentException("Maximum of 5 attachments allowed", nameof(attachmentIds));
		}

		List<TaskAttachment> pendingAttachments = await context.TaskAttachments
			.Where(attachment => uniqueIds.Contains(attachment.Id) &&
								 attachment.UserId == userId &&
								 attachment.TaskItemId == null)
			.ToListAsync();

		if (pendingAttachments.Count != uniqueIds.Count)
		{
			throw new ArgumentException("One or more attachments is invalid", nameof(attachmentIds));
		}

		foreach (TaskAttachment attachment in pendingAttachments)
		{
			attachment.TaskItemId = task.Id;
			attachment.Task = task;

			if (!task.TaskAttachments.Any(existing => existing.Id == attachment.Id))
			{
				task.TaskAttachments.Add(attachment);
			}
		}
	}

	/// <summary>
	/// Deletes a task and any nested child tasks owned by the same user.
	/// </summary>
	/// <returns>True when a task was deleted; otherwise false.</returns>
	public async Task<bool> DeleteTaskAsync(int userId, int id)
	{
		await using var context = await _dbContextFactory.CreateDbContextAsync();
		var task = await context.TaskItems
			.Include(t => t.Children)
			.ThenInclude(t => t.Children)
			.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

		if (task == null)
		{
			return false;
		}

		var descendants = task.Children
			.SelectMany(child => child.Children.Prepend(child))
			.Where(descendant => descendant.UserId == userId)
			.ToList();

		context.TaskItems.RemoveRange(descendants);
		context.TaskItems.Remove(task);
		await context.SaveChangesAsync();
		return true;
	}

	/// <summary>
	/// Fetches the count of tasks which are visible for the requesting user.
	/// </summary>
	/// <returns>A StatusCountResponse of all visible tasks or zeros if there are none</returns>
	public async Task<StatusCountResponse> GetTaskStatusCountsAsync(int requestingUserId, int requestedUserId)
	{
		await using var context = await _dbContextFactory.CreateDbContextAsync();
		IQueryable<TaskItem> query = context.TaskItems
			.Where(t => t.UserId == requestedUserId);

		if (requestingUserId != requestedUserId)
		{
			var statuses = await _followService.GetRelationshipStatusesAsync(requestingUserId, new[] { requestedUserId });
			bool isFriend = statuses[requestedUserId] == RelationshipStatus.Friends;
			query = query.Where(t =>
				t.Visibility == TaskVisibility.Public ||
				(t.Visibility == TaskVisibility.FriendsOnly && isFriend));
		}

		var now = _timeProvider.GetLocalNow().DateTime;
		var today = DateOnly.FromDateTime(now);
		var nowTime = TimeOnly.FromDateTime(now);

		var counts = await query
			.GroupBy(_ => 1)
			.Select(g => new StatusCountResponse
			{
				Todo = g.Count(t => t.ItemStatus == TaskItemStatus.Todo &&
					(t.DueDate > today || (t.DueDate == today && t.DueTime >= nowTime))),
				InProgress = g.Count(t => t.ItemStatus == TaskItemStatus.InProgress &&
					(t.DueDate > today || (t.DueDate == today && t.DueTime >= nowTime))),
				Done = g.Count(t => t.ItemStatus == TaskItemStatus.Done &&
					(t.DueDate > today || (t.DueDate == today && t.DueTime >= nowTime))),
				Overdue = g.Count(t => t.ItemStatus != TaskItemStatus.Done &&
					(t.DueDate < today || (t.DueDate == today && t.DueTime < nowTime)))
			})
			.FirstOrDefaultAsync();

		return counts ?? new StatusCountResponse { Todo = 0, InProgress = 0, Done = 0, Overdue = 0 };
	}

	/// <summary>
	/// Returns previews for the given tasks, keyed by task ID. Tasks that don't exist or
	/// aren't visible to the viewer are omitted from the result.
	/// </summary>
	public async Task<Dictionary<int, SharedTaskPreview>> GetSharedTaskPreviewsAsync(
		int viewerId, IReadOnlyCollection<int> taskIds)
	{
		if (taskIds.Count == 0)
			return new();

		await using var context = await _dbContextFactory.CreateDbContextAsync();

		var tasks = await context.TaskItems.AsNoTracking()
			.Include(t => t.TaskAttachments)
			.Where(t => taskIds.Contains(t.Id))
			.ToListAsync();

		var ownerIds = tasks.Select(t => t.UserId).Distinct().ToList();

		var ownerNames = await context.Users.AsNoTracking()
			.Where(u => ownerIds.Contains(u.Id))
			.ToDictionaryAsync(u => u.Id, u => u.DisplayName);

		var otherOwnerIds = tasks
			.Select(t => t.UserId)
			.Where(id => id != viewerId)
			.Distinct()
			.ToArray();

		var statuses = await _followService.GetRelationshipStatusesAsync(viewerId, otherOwnerIds);

		bool IsVisible(TaskItem t) =>
			t.UserId == viewerId
			|| t.Visibility == TaskVisibility.Public
			|| (t.Visibility == TaskVisibility.FriendsOnly
				&& statuses.TryGetValue(t.UserId, out var s)
				&& s == RelationshipStatus.Friends);

		return tasks.Where(IsVisible).ToDictionary(t => t.Id, t => new SharedTaskPreview
		{
			Id = t.Id,
			UserId = t.UserId,
			UserDisplayName = ownerNames.GetValueOrDefault(t.UserId, "Unknown user"),
			Title = t.Title,
			IsClip = t.IsClip,
			VideoAttachment = t.TaskAttachments
				.Where(a => a.ContentType.StartsWith("video/"))
				.Select(AttachmentResponse.FromEntity)
				.FirstOrDefault(),
			ImageAttachment = t.TaskAttachments
				.Where(a => a.ContentType.StartsWith("image/"))
				.Select(AttachmentResponse.FromEntity)
				.FirstOrDefault(),
		});
	}

}
