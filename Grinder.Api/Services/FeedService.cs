using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Services;

public interface IFeedService
{
    Task<FeedPageDto> GetFriendsFeedAsync(int userId, int page, int limit);
    Task MarkTasksAsViewedAsync(int userId, IEnumerable<int> taskItemIds);
}

/// <summary>
/// Responsible for managing the reverse-chronological feed of a user's friends' tasks
/// </summary>

public class FeedService : IFeedService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IReactionService _reactionService;

    public FeedService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider, IReactionService reactionService)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _reactionService = reactionService;
    }

    /// <summary>
    /// Returns a page of public/friends-only tasks from the given user's mutual friends, newest first.
    /// </summary>
    public async Task<FeedPageDto> GetFriendsFeedAsync(int userId, int page, int limit)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        limit = Math.Clamp(limit, 1, 50);
        page = Math.Max(page, 1);

        var followingIds = await context.Follow
            .AsNoTracking()
            .Where(f => f.FollowerId == userId)
            .Select(f => f.FollowedId)
            .ToListAsync();

        var mutualFriendIds = await context.Follow
            .AsNoTracking()
            .Where(f => f.FollowerId == userId &&
                        context.Follow.Any(r => r.FollowerId == f.FollowedId && r.FollowedId == userId))
            .Select(f => f.FollowedId)
            .ToListAsync();

        // Shared "visible following post" filter, reused by both the unseen and seen tiers
        IQueryable<TaskItem> FollowingBase() => context.TaskItems
            .AsNoTracking()
            .Where(t => followingIds.Contains(t.UserId)
                        && t.ParentTaskId == null
                        && (t.Visibility == TaskVisibility.Public
                            || (t.Visibility == TaskVisibility.FriendsOnly && mutualFriendIds.Contains(t.UserId))));

        // Tier 1: following posts not yet seen (or updated since last view)
        var primaryQuery = FollowingBase()
            .Where(t => !context.TaskViews.Any(v => v.UserId == userId
                                                      && v.TaskItemId == t.Id
                                                      && v.ViewedAt >= t.UpdatedAt))
            .OrderByDescending(t => t.Id);

        var userCategoryIds = await context.Category
            .AsNoTracking()
            .Where(c => c.Tasks.Any(t => t.UserId == userId))
            .Select(c => c.Id)
            .Distinct()
            .ToListAsync();

        // Tier 2: discovery pool, unseen (unchanged)
        var publicQuery = context.TaskItems
            .AsNoTracking()
            .Where(t => t.UserId != userId
                        && !followingIds.Contains(t.UserId)
                        && t.ParentTaskId == null
                        && t.Visibility == TaskVisibility.Public
                        && !context.TaskViews.Any(v => v.UserId == userId
                                                        && v.TaskItemId == t.Id
                                                        && v.ViewedAt >= t.UpdatedAt))
            .OrderByDescending(t => t.Categories.Count(c => userCategoryIds.Contains(c.Id)))
            .ThenByDescending(t => t.Id);

        // Tier 3: following posts already seen — only surfaced once tiers 1 & 2 run dry
        var seenFollowingQuery = FollowingBase()
            .Where(t => context.TaskViews.Any(v => v.UserId == userId
                                                     && v.TaskItemId == t.Id
                                                     && v.ViewedAt >= t.UpdatedAt))
            .OrderByDescending(t => t.Id);

        var primaryTotal = await primaryQuery.CountAsync();
        var publicTotal = await publicQuery.CountAsync();
        var seenFollowingTotal = await seenFollowingQuery.CountAsync();
        var totalCount = primaryTotal + publicTotal + seenFollowingTotal;

        var skip = (page - 1) * limit;
        var tasks = new List<TaskItem>();
        var remainingSkip = skip;
        var remainingTake = limit;

        // Pulls from a tier only after prior tiers (and this one's skip offset) are exhausted
        async Task ConsumeTier(IQueryable<TaskItem> query, int tierTotal)
        {
            if (remainingTake <= 0) return;

            if (remainingSkip >= tierTotal)
            {
                remainingSkip -= tierTotal;
                return;
            }

            var tierTasks = await query
                .Skip(remainingSkip)
                .Take(remainingTake)
                .Include(t => t.User)
                .ThenInclude(t => t.SelectedBadge)
                .Include(t => t.TaskAttachments)
                .Include(t => t.Categories)
                .Include(t => t.Reactions)
                .ThenInclude(r => r.Reaction)
                .ToListAsync();

            tasks.AddRange(tierTasks);
            remainingTake -= tierTasks.Count;
            remainingSkip = 0; // once we've started pulling from a tier, the offset is spent
        }

        await ConsumeTier(primaryQuery, primaryTotal);
        await ConsumeTier(publicQuery, publicTotal);
        await ConsumeTier(seenFollowingQuery, seenFollowingTotal);

        var taskIds = tasks.Select(t => t.Id).ToList();

        var viewMap = await context.TaskViews
            .AsNoTracking()
            .Where(v => v.UserId == userId && taskIds.Contains(v.TaskItemId))
            .ToDictionaryAsync(v => v.TaskItemId, v => v.ViewedAt);

        var items = tasks.Select(t =>
        {
            bool isUpdated = viewMap.TryGetValue(t.Id, out var viewedAt)
                && viewedAt < t.UpdatedAt
                && t.UpdatedAt > t.CreatedAt;

            return new FeedItemDto
            {
                Id = t.Id,
                UserId = t.UserId,
                UserDisplayName = t.User!.DisplayName,
                Title = t.Title,
                Description = t.Description,
                CreatedAt = t.IsClip ? (t.ClipCreatedAt ?? t.CreatedAt) : t.CreatedAt,
                IsUpdated = isUpdated,
                ChangedFields = isUpdated ? t.ChangedFields : null,
                StartDate = t.StartDate,
                StartTime = t.StartTime,
                SelectedBadgeIcon = t.User.SelectedBadge?.Icon,
                SelectedBadgeName = t.User.SelectedBadge?.Name,
                DueDate = t.DueDate,
                DueTime = t.DueTime,
                ItemStatus = t.ItemStatus,
                TaskAttachments = t.TaskAttachments.Select(AttachmentResponse.FromEntity).ToList(),
                Reactions = _reactionService.GetReactionsForTaskAsync(t.Reactions, userId).Result,
                Categories = t.Categories.Select(TaskCategoryResponse.FromEntity).ToList()
            };
        }).ToList();

        return new FeedPageDto
        {
            Items = items,
            Page = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)limit)
        };
    }

    // <summary>
    // 	sets a list of tasks to be marked as viewed and the timestamp when they were viewed
    // </summary>
    public async Task MarkTasksAsViewedAsync(int userId, IEnumerable<int> taskItemIds)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var ids = taskItemIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var validIds = await context.TaskItems
            .Where(t => ids.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync();

        if (validIds.Count == 0) return;

        var existing = await context.TaskViews
            .Where(v => v.UserId == userId && validIds.Contains(v.TaskItemId))
            .ToDictionaryAsync(v => v.TaskItemId);

        var now = _timeProvider.GetUtcNow();

        foreach (var id in ids)
        {
            if (existing.TryGetValue(id, out var view))
            {
                view.ViewedAt = now;
            }
            else
            {
                context.TaskViews.Add(new TaskView { UserId = userId, TaskItemId = id, ViewedAt = now });
            }
        }

        await context.SaveChangesAsync();
    }

}
