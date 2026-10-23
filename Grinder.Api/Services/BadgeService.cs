using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Services;

public interface IBadgeService
{
    Task<IEnumerable<BadgeResponse>> GetAllBadgesForUserAsync(int userId);
    Task EvaluateBadgesAsync(int userId);
    Task<bool> SelectBadgeAsync(int userId, int? badgeId);
}

/// <summary>
/// Service responsible for managing badges, including retrieving badge information, 
/// evaluating user activity to award badges, and allowing users to select a badge to 
/// display on their profile.
/// </summary>
public class BadgeService : IBadgeService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IBadgeProgressEvaluator _progressEvaluator;

    public BadgeService(
        IDbContextFactory<DatabaseContext> dbContextFactory,
        TimeProvider timeProvider,
        IBadgeProgressEvaluator progressEvaluator)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _progressEvaluator = progressEvaluator;
    }

    /// <summary>
    /// Retrieves all badges and their earned status for a specific user.
    /// Each badge will indicate whether it has been unlocked by the user and, if so, when it was earned.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IEnumerable<BadgeResponse>> GetAllBadgesForUserAsync(int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var badges = await context.Badges.ToListAsync();

        var earnedBadges = await context.UserBadges
            .Where(ub => ub.UserId == userId)
            .ToDictionaryAsync(ub => ub.BadgeId, ub => ub.EarnedAt);

        var stats = await _progressEvaluator.ComputeStatsAsync(context, userId);

        return badges.Select(badge => new BadgeResponse
        {
            Id = badge.Id,
            Name = badge.Name,
            Icon = badge.Icon,
            Description = badge.Description,
            IsUnlocked = earnedBadges.ContainsKey(badge.Id),
            EarnedAt = earnedBadges.ContainsKey(badge.Id) ? earnedBadges[badge.Id] : null,
            Progress = Math.Min(_progressEvaluator.GetCurrentValue(badge, stats), badge.Threshold),
            Threshold = badge.Threshold
        });
    }

    /// <summary>
    /// Evaluates and awards badges to a user based on their activity.
    /// This method checks user's progress against the badge criteria and awards any new badges that the user qualifies for.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task EvaluateBadgesAsync(int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var unearnedBadges = await context.Badges
            .Where(b => !context.UserBadges.Any(ub => ub.UserId == userId && ub.BadgeId == b.Id))
            .ToListAsync();

        if (unearnedBadges.Count == 0)
        {
            return;
        }
        var stats = await _progressEvaluator.ComputeStatsAsync(context, userId);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach(var badge in unearnedBadges)
        {
            if (_progressEvaluator.IsCriteriaMet(badge, stats))
            {
                context.UserBadges.Add(new UserBadge
                {
                    UserId = userId,
                    BadgeId = badge.Id,
                    EarnedAt = now
                });
            }
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Safeguard against another process having awarded the same badge concurrently.
            // The unique constraint on UserId+BadgeId is the only failure mode we can safely ignore here.
        }

    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException switch
        {
            PostgresException pg => pg.SqlState == PostgresErrorCodes.UniqueViolation,
            SqliteException sqlite => sqlite.SqliteErrorCode == 19, // SQLITE_CONSTRAINT
            _ => false
        };
    }

    /// <summary>
    /// Allows a user to select a badge to display on their profile.
    /// If the user has not earned the specified badge, the selection will not be applied.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="badgeId"></param>
    /// <returns>True if the badge was successfully selected, false otherwise.</returns>
    public async Task<bool> SelectBadgeAsync(int userId, int? badgeId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var user = await context.Users.FindAsync(userId);

        if (user == null) return false;
 
        if (badgeId == null)
        {
            user.SelectedBadgeId = null;
            await context.SaveChangesAsync();
            return true;
        }
 
        var hasEarned = await context.UserBadges
            .AnyAsync(ub => ub.UserId == userId && ub.BadgeId == badgeId.Value);
 
        if (!hasEarned) return false;
 
        user.SelectedBadgeId = badgeId.Value;
        await context.SaveChangesAsync();
        return true;
    }
}