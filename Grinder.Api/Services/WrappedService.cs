using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Services;

public interface IWrappedService
{
    Task<int?> GetMostRecentEligibleYearAsync(int userId);
    Task<int?> GetCurrentEligibleYearAsync(int userId);
    Task<bool> IsDismissedAsync(int userId, int year);
    Task DismissAsync(int userId, int year);
    Task<WrappedStatsResponse> GetWrappedStatsAsync(int userId, int year);
    Task<bool> IsEligibleForConstantCurrentYearAsync(int userId);
    Task<bool> IsEligibleForConstantPreviousYearAsync(int userId);
}

public class WrappedService : IWrappedService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public WrappedService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    

    /// <summary>
    /// Returns the most recent year for which the user is eligible for a wrapped.
    /// </summary>
    /// <returns>
    /// The most recent year for which the user is eligible for a wrapped, or null 
    /// if no such year exists.
    /// </returns>
    /// <param name="userId">The ID of the user to check.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the user with the specified ID is not found.
    /// </exception>
    /// <remarks>
    /// A user is eligible for a wrapped if they have completed tasks in that year,
    /// and their account was created more than 2 months before the start of that year.
    /// </remarks>
    public async Task<int?> GetMostRecentEligibleYearAsync(int userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found.");
        }

        int latestReleasedYear = LatestReleasedYear();
        for (int year = latestReleasedYear; year >= user.CreatedAt.Year; year--)
        {
            if (await IsEligibleForYearAsync(dbContext, user, userId, year))
            {
                return year;
            }
        }

        return null;
    }

    ///<summary>
    ///Returns the current year's Wrapped if the user is eligible for it, otherwise null.
    ///</summary>
    ///<param name="userId">The ID of the user to check.</param>
    ///<returns>
    ///The current Wrapped year if it is in its release window and the user is eligible for it,
    ///otherwise null.
    ///</returns>
    ///<exception cref="ArgumentException">
    ///Thrown when the user with the specified ID is not found.
    ///</exception>
    public async Task<int?> GetCurrentEligibleYearAsync(int userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found.");
        }

        int currentYear = LatestReleasedYear();
        return await IsEligibleForYearAsync(dbContext, user, userId, currentYear)
            ? currentYear
            : null;
    }

    /// <summary>
    /// Returns the latest Wrapped year that has been released.
    /// </summary>
    private int LatestReleasedYear()
    {
        var localNow = _timeProvider.GetLocalNow();
        return localNow.Month == 12 ? localNow.Year : localNow.Year - 1;
    }

    /// <summary>
    /// Returns whether the user is eligible for the Wrapped of a specific year: their account
    /// was created at least 2 months before that year's window, and they completed at least one
    /// task within the 2-month activity window ending at the start of that December.
    /// </summary>
    private static async Task<bool> IsEligibleForYearAsync(
        DatabaseContext dbContext, User user, int userId, int year)
    {
        var resetDate = new DateTimeOffset(year, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var accountAgeCutoff = resetDate.AddMonths(-2); // 1 October Y
        if (user.CreatedAt >= accountAgeCutoff)
        {
            return false;
        }

        var activityWindowStart = new DateTimeOffset(year - 1, 12, 1, 0, 0, 0, TimeSpan.Zero);
        return await dbContext.TaskItems.AnyAsync(t =>
            t.UserId == userId
            && t.ItemStatus == TaskItemStatus.Done
            && t.CompletedAt != null
            && t.CompletedAt >= activityWindowStart
            && t.CompletedAt < resetDate);
    }

    ///<summary>
    ///Returns whether the user has dismissed the Wrapped popup for the given year.
    ///</summary>
    ///<param name="userId">The ID of the user to check.</param>
    ///<param name="year">The Wrapped year to check.</param>
    ///<returns>
    ///True if the user has dismissed the Wrapped popup for the given year, false otherwise.
    ///</returns>
    ///<exception cref="ArgumentException">
    ///Thrown when the user with the specified ID is not found.
    ///</exception>
    public async Task<bool> IsDismissedAsync(int userId, int year)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.WrappedDismissals
            .AnyAsync(d => d.UserId == userId && d.Year == year);
    }

    ///<summary>
    ///Records that the user has dismissed the Wrapped popup for the given year.
    ///Idempotent: calling it again for the same (user, year) is a no-op.
    ///</summary>
    ///<param name="userId">The ID of the user dismissing the popup.</param>
    ///<param name="year">The Wrapped year being dismissed.</param>
    public async Task DismissAsync(int userId, int year)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        bool alreadyDismissed = await dbContext.WrappedDismissals
            .AnyAsync(d => d.UserId == userId && d.Year == year);

        if (alreadyDismissed)
        {
            return;
        }

        dbContext.WrappedDismissals.Add(new WrappedDismissal
        {
            UserId = userId,
            Year = year,
            DismissedAt = _timeProvider.GetUtcNow()
        });

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            //A request at the same time might have inserted the same row first
        }
    }

    public async Task<WrappedStatsResponse> GetWrappedStatsAsync(int userId, int year)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new ArgumentException($"User not found: {userId}");
        }

        // EF cannot translate DateTimeOffset.Year to SQL, so filter with a half-open year range instead.
        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var nextYearStart = yearStart.AddYears(1);

        var tasksStarted = await dbContext.TaskItems.CountAsync(t =>
            t.UserId == userId
            && t.CreatedAt >= yearStart
            && t.CreatedAt < nextYearStart);

        var tasksCompleted = await dbContext.TaskItems.CountAsync(t =>
            t.UserId == userId
            && t.ItemStatus == TaskItemStatus.Done
            && t.CompletedAt != null
            && t.CompletedAt >= yearStart
            && t.CompletedAt < nextYearStart);


        var completionPercent = tasksStarted == 0
            ? 0
            : (int)Math.Round(100.0 * tasksCompleted / tasksStarted);


        var mostLikedTask = await dbContext.TaskItems
            .Where(t =>
                t.UserId == userId
                && t.CreatedAt >= yearStart
                && t.CreatedAt < nextYearStart
                && t.Reactions.Count > 0)
            .OrderByDescending(t => t.Reactions.Count)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new WrappedMostReactedToResponse(t.Id, t.Title, t.Reactions.Count))
            .FirstOrDefaultAsync();


        // Comments have no reaction table; reply count is the stand-in for "most liked". TODO: clarify w Fabian
        var mostLikedComment = await dbContext.Comments
            .Where(c =>
                c.AuthorId == user.Id
                && !c.IsDeleted
                && c.CreatedAt >= yearStart
                && c.CreatedAt < nextYearStart
                && c.Replies.Count > 0)
            .OrderByDescending(c => c.Replies.Count)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new WrappedMostRepliedToResponse(c.Id, c.TaskId, c.Content, c.Replies.Count))
            .FirstOrDefaultAsync();

        // Order on an anonymous type first - EF cannot translate OrderBy on WrappedCategoryStatResponse after GroupBy - annnoying ik
        var topCategories = await dbContext.Category
            .Select(c => new
            {
                c.Name,
                TaskCount = c.Tasks.Count(t =>
                    t.UserId == userId
                    && t.CreatedAt >= yearStart
                    && t.CreatedAt < nextYearStart)
            })
            .Where(c => c.TaskCount > 0)
            .OrderByDescending(c => c.TaskCount)
            .ThenBy(c => c.Name)
            .Take(5)
            .Select(c => new WrappedCategoryStatResponse(c.Name, c.TaskCount))
            .ToListAsync();

        var personalStats = await GetWrappedExtraStatsAsync(userId, year);

        return new WrappedStatsResponse(
            tasksStarted,
            tasksCompleted,
            completionPercent,
            mostLikedTask,
            mostLikedComment,
            topCategories,
            personalStats
        );
    }
    
    public async Task<WrappedExtraStats> GetWrappedExtraStatsAsync(int userId, int year)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new ArgumentException($"User not found: {userId}");
        }

        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var nextYearStart = yearStart.AddYears(1);
        var dateTimeYearStart = yearStart.UtcDateTime;
        var dateTimeNextYearStart = nextYearStart.UtcDateTime;

        var badgesEarnedCount = await dbContext.UserBadges.CountAsync(
            b => b.UserId == userId &&
            b.EarnedAt >= dateTimeYearStart &&
            b.EarnedAt < dateTimeNextYearStart);
            
        var friendsMadeCount = await dbContext.Follow
            .AsNoTracking()
            .Where(f =>
                f.FollowerId == userId &&
                dbContext.Follow.Any(reverse =>
                    reverse.FollowerId == f.FollowedId &&
                    reverse.FollowedId == userId) &&
                f.CreatedAt >= dateTimeYearStart &&     
                f.CreatedAt < dateTimeNextYearStart)
            .CountAsync();  
        
        var newFollowersCount = await dbContext.Follow
            .AsNoTracking()
            .Where(f =>
                f.FollowedId == userId &&
                f.CreatedAt >= dateTimeYearStart &&
                f.CreatedAt < dateTimeNextYearStart)
            .CountAsync();

        var messagesSentCount = await dbContext.DirectMessages
            .AsNoTracking()
            .Where(m =>
                m.SenderId == userId &&
                m.SentAt >= yearStart &&
                m.SentAt < nextYearStart)
            .CountAsync();
        
        var sharedCount = await dbContext.DirectMessages
            .AsNoTracking()
            .Where(m =>
                m.SenderId == userId &&
                m.SharedTaskId != null &&
                m.SentAt >= yearStart &&
                m.SentAt < nextYearStart)
            .CountAsync();

        var commentsWrittenCount = await dbContext.Comments
            .AsNoTracking()
            .Where(c =>
                c.AuthorId == userId &&
                !c.IsDeleted &&
                c.CreatedAt >= yearStart &&
                c.CreatedAt < nextYearStart)
            .CountAsync();

        var reactionsReceivedCount = await dbContext.TaskReactions
            .AsNoTracking()
            .Where(r =>
                r.TaskItem != null &&
                r.TaskItem.UserId == userId &&
                r.CreatedAt >= yearStart &&
                r.CreatedAt < nextYearStart)
            .CountAsync();

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var tasksOverdueCount = await dbContext.TaskItems
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId &&
                t.ItemStatus != TaskItemStatus.Done &&
                t.CreatedAt >= yearStart &&
                t.CreatedAt < nextYearStart &&
                t.DueDate < today)
            .CountAsync();

        var completedTaskDates = await dbContext.TaskItems
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId &&
                t.ItemStatus == TaskItemStatus.Done &&
                t.CompletedAt != null &&
                t.CompletedAt >= yearStart &&
                t.CompletedAt < nextYearStart)
            .Select(t => t.CompletedAt!.Value)
            .ToListAsync();

        var mostProductiveMonthGroup = completedTaskDates
            .GroupBy(completedAt => completedAt.Month)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .FirstOrDefault();

        var mostProductiveMonth = mostProductiveMonthGroup is not null
            ? new WrappedProductiveMonthResponse(
                new DateTime(year, mostProductiveMonthGroup.Key, 1).ToString("MMMM"), // now uses the correct year
                mostProductiveMonthGroup.Count())
            : null;
        
        return new WrappedExtraStats(
            badgesEarnedCount,
            friendsMadeCount,
            newFollowersCount,
            messagesSentCount,
            sharedCount,
            commentsWrittenCount,
            reactionsReceivedCount,
            tasksOverdueCount,
            mostProductiveMonth
        );
    }

    /// <summary>
    /// Returns whether the user is eligible for the Wrapped of the current year: their account
    /// was created at least 2 months before the start of December of the current year,
    /// and they have completed at least one task in the current year.
    /// </summary>
    /// <param name="userId"> The ID of the user to check. </param>
    /// <returns> True if the user is eligible for the current year's Wrapped, false otherwise. </returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> IsEligibleForConstantCurrentYearAsync(int userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found.");
        }

        var now = _timeProvider.GetUtcNow();
        if (user.CreatedAt > now.AddMonths(-2))
        {
            return false;
        }

        return await HasCompletedTaskInCalendarYearAsync(dbContext, userId, now.Year);
    }

    /// <summary>
    /// Returns whether the user is eligible for the Wrapped of the previous year: 
    /// they have completed at least one task in the previous year.
    /// </summary>
    /// <param name="userId"> The ID of the user to check. </param>
    /// <returns> True if the user is eligible for the previous year's Wrapped, false otherwise. </returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> IsEligibleForConstantPreviousYearAsync(int userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new ArgumentException($"User with ID {userId} not found.");
        }

        var now = _timeProvider.GetUtcNow();

        return await HasCompletedTaskInCalendarYearAsync(dbContext, userId, now.Year - 1);
    }

    /// <summary>
    /// Returns whether the user has completed at least one task in the specified calendar year.
    /// </summary>
    /// <param name="dbContext"> The database context. </param>
    /// <param name="userId"> The ID of the user to check. </param>
    /// <param name="year"> The calendar year to check. </param>
    /// <returns> True if the user has completed at least one task in the specified calendar year, false otherwise. </returns>
    private static async Task<bool> HasCompletedTaskInCalendarYearAsync(
        DatabaseContext dbContext, int userId, int year)
    {
        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var nextYearStart = yearStart.AddYears(1);

        return await dbContext.TaskItems.AnyAsync(t =>
            t.UserId == userId
            && t.ItemStatus == TaskItemStatus.Done
            && t.CompletedAt != null
            && t.CompletedAt >= yearStart
            && t.CompletedAt < nextYearStart);
    }
}