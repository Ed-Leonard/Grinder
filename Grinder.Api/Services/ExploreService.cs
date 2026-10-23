using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Services;

public interface IExploreService
{
    Task<ExplorePage> GetExploreFeedAsync(int userId, int page, int limit);
}

/// <summary>
/// Responsible for managing the reverse-chronological feed of a user's friends' tasks
/// </summary>

public class ExploreService : IExploreService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IReactionService _reactionService;

    public ExploreService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider, IReactionService reactionService)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _reactionService = reactionService;
    }

    /// <summary>
    /// Returns a page of public tasks ranked by their interaction count. Unviewed tasks are prioritised first. 
    /// </summary>
	public async Task<ExplorePage> GetExploreFeedAsync(int userId, int page, int limit)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        limit = Math.Clamp(limit, 1, 50);
        page = Math.Max(page, 1);

        var since = _timeProvider.GetUtcNow().AddHours(-24);


        // lots of DB type issues here but this works and I don't want to touch it
        var reactionCounts = await context.TaskReactions
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.TaskItemId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count);

        var commentCounts = await context.Comments
            .Where(c => c.CreatedAt >= since && !c.IsDeleted)
            .GroupBy(c => c.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count);

        int Score(int taskId) =>
            (reactionCounts.TryGetValue(taskId, out var r) ? r : 0) +
            (commentCounts.TryGetValue(taskId, out var c) ? c : 0);

        IQueryable<TaskItem> ExploreBase() => context.TaskItems
            .AsNoTracking()
            .Where(t => t.UserId != userId && t.Visibility == TaskVisibility.Public);

        // Just fetch IDs here — no scoring in SQL at all
        var unseenIds = await ExploreBase()
            .Where(t => !context.TaskViews.Any(v => v.UserId == userId && v.TaskItemId == t.Id && v.ViewedAt >= t.UpdatedAt))
            .Select(t => t.Id)
            .ToListAsync();

        var seenIds = await ExploreBase()
            .Where(t => context.TaskViews.Any(v => v.UserId == userId && v.TaskItemId == t.Id && v.ViewedAt >= t.UpdatedAt))
            .Select(t => t.Id)
            .ToListAsync();

        // Score + order in memory
        var unseenOrdered = unseenIds.OrderByDescending(Score).ThenByDescending(id => id).ToList();
        var seenOrdered = seenIds.OrderByDescending(Score).ThenByDescending(id => id).ToList();

        var unseenTotal = unseenOrdered.Count;
        var seenTotal = seenOrdered.Count;
        var totalCount = unseenTotal + seenTotal;

        var skip = (page - 1) * limit;
        var pageIds = new List<int>();
        var remainingSkip = skip;
        var remainingTake = limit;

        void ConsumeTier(List<int> tierIds)
        {
            if (remainingTake <= 0) return;
            if (remainingSkip >= tierIds.Count) { remainingSkip -= tierIds.Count; return; }

            var slice = tierIds.Skip(remainingSkip).Take(remainingTake).ToList();
            pageIds.AddRange(slice);
            remainingTake -= slice.Count;
            remainingSkip = 0;
        }

        ConsumeTier(unseenOrdered);
        ConsumeTier(seenOrdered);

        // Fetch full entities for just this page, preserving score order
        var idOrder = pageIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        var tasks = await context.TaskItems
            .AsNoTracking()
            .Where(t => pageIds.Contains(t.Id))
            .Include(t => t.TaskAttachments)
            .ToListAsync();

        tasks = tasks.OrderBy(t => idOrder[t.Id]).ToList();

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

            return new ExploreItem
            {
                Id = t.Id,
                Title = t.Title,
                TaskAttachments = t.TaskAttachments.Select(AttachmentResponse.FromEntity).ToList(),
            };
        }).ToList();

        return new ExplorePage
        {
            Items = items,
            Page = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)limit)
        };
    }
}
