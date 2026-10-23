namespace Grinder.Api.Services;

using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;

public interface IReactionService
{
    Task<IEnumerable<Reaction>> GetReactionsAsync();
    Task<ReactionResponse> ToggleReactionAsync(int taskId, int reactionId, int userId);
    Task<List<ReactionResponse>> GetReactionsForTaskAsync(ICollection<TaskReaction> reactions, int userId);
}

/// <summary>
/// Provides functionality for managing task reactions.
/// </summary>
public class ReactionService : IReactionService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly INotificationService _notificationService;
    private readonly IFollowService _followService;

    public ReactionService(IDbContextFactory<DatabaseContext> dbContextFactory, INotificationService notificationService, IFollowService followService)
    {
        _dbContextFactory = dbContextFactory;
        _notificationService = notificationService;
        _followService = followService;
    }

    /// <summary>
    /// Toggles a reaction on a task for a user.
    /// </summary>
    /// <param name="taskId">The ID of the task.</param>
    /// <param name="reactionId">The ID of the reaction to toggle.</param>
    /// <param name="userId">The ID of the user toggling the reaction.</param>
    /// <returns>
    /// A reaction response containing the updated reaction count
    /// and whether the user has reacted.
    /// </returns>
    public async Task<ReactionResponse> ToggleReactionAsync(int taskId, int reactionId, int currentUserId)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var reaction = await dbContext.Reactions.FirstOrDefaultAsync(r => r.Id == reactionId);
        if (reaction == null)
        {
            throw new KeyNotFoundException($"Reaction {reactionId} not found");
        }

        var existingCount = await dbContext.TaskReactions
            .CountAsync(t => t.TaskItemId == taskId && t.ReactionId == reactionId);

        var currentTaskReaction = await dbContext.TaskReactions
            .FirstOrDefaultAsync(t => t.TaskItemId == taskId && t.ReactionId == reactionId && t.UserId == currentUserId);

        var task = await dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null)
        {
            throw new KeyNotFoundException($"Task {taskId} not found");
        }

        await CheckVisibilityAsync(task, currentUserId);

        var response = new ReactionResponse
        {
            Reaction = new ReactionItemDto(reaction.Id, reaction.Name, reaction.FileName),
            Count = existingCount,
            ReactedByCurrentUser = false,
        };

        if (currentTaskReaction == null)
        {
            dbContext.TaskReactions.Add(new TaskReaction
            {
                TaskItemId = taskId,
                ReactionId = reactionId,
                UserId = currentUserId,
                CreatedAt = DateTime.UtcNow,
            });
            response.Count += 1;
            response.ReactedByCurrentUser = true;
        }
        else
        {
            dbContext.TaskReactions.Remove(currentTaskReaction);
            response.Count -= 1;
        }

        await dbContext.SaveChangesAsync();
        if (currentTaskReaction == null)
        {
            await _notificationService.CreateAsync(task.UserId, currentUserId, NotificationType.React, taskId, reaction: new ReactionItemDto(reaction.Id, reaction.Name, reaction.FileName));
        }
        return response;
    }

    /// <summary>
    /// Gets all available reactions.
    /// </summary>
    /// <returns>
    /// A collection containing all available reactions.
    /// </returns>
    public async Task<IEnumerable<Reaction>> GetReactionsAsync()
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Reactions.ToListAsync();
    }

    /// <summary>
    /// Ensures the given user is allowed to react to the given task: the task must
    /// belong to the user, be Public, or be FriendsOnly with the two users as friends.
    /// </summary>
    private async Task CheckVisibilityAsync(TaskItem task, int currentUserId)
    {
        if (task.UserId == currentUserId || task.Visibility == TaskVisibility.Public)
        {
            return;
        }

        if (task.Visibility == TaskVisibility.FriendsOnly)
        {
            var statuses = await _followService.GetRelationshipStatusesAsync(currentUserId, new[] { task.UserId });
            var isFriend = statuses.TryGetValue(task.UserId, out var status) && status == RelationshipStatus.Friends;

            if (isFriend)
            {
                return;
            }
        }

        throw new UnauthorizedAccessException("You do not have permission to react to this task");
    }


    public async Task<List<ReactionResponse>> GetReactionsForTaskAsync(ICollection<TaskReaction> reactions, int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return reactions.GroupBy(r => r.ReactionId)
                .Select(g => new ReactionResponse
                {
                    Reaction = new ReactionItemDto(g.First().Reaction!.Id, g.First().Reaction!.Name, g.First().Reaction!.FileName),
                    Count = g.Count(),
                    ReactedByCurrentUser = g.Any(r => r.UserId == userId),
                })
                .ToList() ?? new List<ReactionResponse>();
    }
}
