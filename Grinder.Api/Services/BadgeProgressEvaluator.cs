using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Services;

/// <summary>
/// The activity counts a user's badge criteria are evaluated against. Everything here must be
/// cheap to compute from data the app already tracks.
/// </summary>
public record BadgeCriteriaStats(
    int TasksDoneCount,
    int TasksCreatedCount,
    int TasksOverdueCount,
    int ClipsCreatedCount,
    int SubtasksCreatedCount,
    int TasksCategorizedCount,
    int FriendsCount);

public interface IBadgeProgressEvaluator
{
    Task<BadgeCriteriaStats> ComputeStatsAsync(DatabaseContext context, int userId);
    int GetCurrentValue(Badge badge, BadgeCriteriaStats stats);
    bool IsCriteriaMet(Badge badge, BadgeCriteriaStats stats);
}

/// <summary>
/// Computes a user's activity stats and reports progress against a badge's criteria.
/// Kept separate from BadgeService so all knowledge of what CriteriaKey means, and how to read
/// it out of a user's stats, stays in one place.
/// </summary>
public class BadgeProgressEvaluator : IBadgeProgressEvaluator
{
    private readonly TimeProvider _timeProvider;
    private readonly IFollowService _followService;

    public BadgeProgressEvaluator(TimeProvider timeProvider, IFollowService followService)
    {
        _timeProvider = timeProvider;
        _followService = followService;
    }

    public async Task<BadgeCriteriaStats> ComputeStatsAsync(DatabaseContext context, int userId)
    {
        var tasksDoneCount = await context.TaskItems
            .CountAsync(t => t.UserId == userId && t.ItemStatus == TaskItemStatus.Done);

        var tasksCreatedCount = await context.TaskItems
            .CountAsync(t => t.UserId == userId);

        var now = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);

        var tasksOverdueCount = await context.TaskItems
            .CountAsync(t => t.UserId == userId && t.ItemStatus != TaskItemStatus.Done &&
                (t.DueDate < today || (t.DueDate == today && t.DueTime < nowTime)));

        var clipsCreatedCount = await context.TaskItems
            .CountAsync(t => t.UserId == userId && t.IsClip);

        var subtasksCreatedCount = await context.TaskItems
            .CountAsync(t => t.UserId == userId && t.ParentTaskId != null);

        var tasksCategorizedCount = await context.TaskItems
            .CountAsync(t => t.UserId == userId && t.Categories.Any());

        var friendsCount = await _followService.GetFriendCountAsync(userId);

        return new BadgeCriteriaStats(
            tasksDoneCount,
            tasksCreatedCount,
            tasksOverdueCount,
            clipsCreatedCount,
            subtasksCreatedCount,
            tasksCategorizedCount,
            friendsCount);
    }

    /// <summary>
    /// Reads the current value of whatever stat a badge's CriteriaKey refers to. This is the
    /// single place CriteriaKey is mapped to data - both IsCriteriaMet and progress reporting
    /// are derived from it, so they can never disagree.
    /// </summary>
    public int GetCurrentValue(Badge badge, BadgeCriteriaStats stats)
    {
        return badge.CriteriaKey switch
        {
            BadgeCriteriaKeys.TasksDone => stats.TasksDoneCount,
            BadgeCriteriaKeys.TasksCreated => stats.TasksCreatedCount,
            BadgeCriteriaKeys.TasksOverdue => stats.TasksOverdueCount,
            BadgeCriteriaKeys.ClipsCreated => stats.ClipsCreatedCount,
            BadgeCriteriaKeys.SubtasksCreated => stats.SubtasksCreatedCount,
            BadgeCriteriaKeys.TasksCategorized => stats.TasksCategorizedCount,
            BadgeCriteriaKeys.FriendsMade => stats.FriendsCount,
            _ => 0
        };
    }

    public bool IsCriteriaMet(Badge badge, BadgeCriteriaStats stats)
    {
        return GetCurrentValue(badge, stats) >= badge.Threshold;
    }
}
