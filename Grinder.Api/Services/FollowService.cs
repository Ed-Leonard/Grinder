using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.DTOs;

namespace Grinder.Api.Services;

public interface IFollowService
{
    Task FollowAsync(int followerId, int followedId);
    Task UnfollowAsync(int followerId, int followedId);
    Task<Dictionary<int, RelationshipStatus>> GetRelationshipStatusesAsync(int currentUserId, IEnumerable<int> targetUserIds);
    Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(int userId, int currentUserId);
    Task<int> GetFriendCountAsync(int userId);
}

public enum RelationshipStatus
{
    Follow,
    Following,
    Friends,
    FollowBack
}

/// <summary>
///  Service responsible for managing follow relationships between users, including following, unfollowing, and checking follow status.
///  Also handles the creation of notifications when a user is followed.
/// </summary>
public class FollowService : IFollowService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly INotificationService _notificationService;

    public FollowService(IDbContextFactory<DatabaseContext> dbContextFactory, INotificationService notificationService)
    {
        _dbContextFactory = dbContextFactory;
        _notificationService = notificationService;
    }

    /// <summary>
    ///   Creates a follow relationship between two users. If the follower is already following the followed user, no action is taken.
    /// </summary>
    /// <param name="followerId"></param>
    /// <param name="followedId"></param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task FollowAsync(int followerId, int followedId)
    {
        if (followerId == followedId)
            throw new InvalidOperationException("You cannot follow yourself.");

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        if (!await context.Users.AnyAsync(u => u.Id == followedId))
            throw new InvalidOperationException("The user you are trying to follow does not exist.");

        var alreadyFollowing = await context.Follow.AnyAsync(f =>
            f.FollowerId == followerId && f.FollowedId == followedId);

        if (alreadyFollowing)
            throw new InvalidOperationException("You already follow this user");

        context.Follow.Add(new Follow
        {
            FollowerId = followerId,
            FollowedId = followedId,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        // recipient = the person being followed, sender = the person doing the following
        await _notificationService.CreateAsync(followedId, followerId, NotificationType.Follow);
    }

    /// <summary>
    ///  Removes a follow relationship between two users. If the follower is not following the followed user, no action is taken.
    /// </summary>
    /// <param name="followerId"></param>
    /// <param name="followedId"></param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task UnfollowAsync(int followerId, int followedId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        // Are we following this user?
        var follow = await context.Follow.FirstOrDefaultAsync(f =>
            f.FollowerId == followerId && f.FollowedId == followedId);

        if (follow is null)
            throw new InvalidOperationException("You are not following this user");

        context.Follow.Remove(follow);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieves the relationship statuses between the current user and a list of target users. The statuses can be Follow, Following, Friends, or FollowBack.
    /// </summary>
    /// <param name="currentUserId">The ID of the current user.</param>
    /// <param name="targetUserIds">A list of target user IDs to retrieve relationship statuses for.</param>
    /// <returns>A dictionary mapping each target user ID to its corresponding relationship status.</returns>
    public async Task<Dictionary<int, RelationshipStatus>> GetRelationshipStatusesAsync(int currentUserId, IEnumerable<int> targetUserIds)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var ids = targetUserIds.ToList();

        var following = await context.Follow.AsNoTracking()
            .Where(f => f.FollowerId == currentUserId && ids.Contains(f.FollowedId))
            .Select(f => f.FollowedId)
            .ToHashSetAsync();

        var followedBy = await context.Follow.AsNoTracking()
            .Where(f => ids.Contains(f.FollowerId) && f.FollowedId == currentUserId)
            .Select(f => f.FollowerId)
            .ToHashSetAsync();

        try
        {
            return ids.ToDictionary(id => id, id => (following.Contains(id), followedBy.Contains(id)) switch
            {
                (true, true) => RelationshipStatus.Friends,
                (true, false) => RelationshipStatus.Following,
                (false, true) => RelationshipStatus.FollowBack,
                (false, false) => RelationshipStatus.Follow
            });
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Duplicate target user IDs found.", ex);
        }
    }

    /// <summary>
    ///  Retrieves the list of friends (users who mutually follow each other) for the given user.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>A list of friends for the given user.</returns>
    public async Task<IReadOnlyList<FriendResponse>> GetFriendsAsync(int userId, int currentUserId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        User? user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            throw new ArgumentException("User not found");
        }

        var friends = await context.Follow.AsNoTracking().Where(f =>
            f.FollowerId == userId &&
            context.Follow.Any(reverse =>
                reverse.FollowerId == f.FollowedId &&
                reverse.FollowedId == userId))
            .OrderBy(f => f.Followed.DisplayName)
            .Select(f => new
            {
                f.FollowedId,
                f.Followed.DisplayName,
                f.Followed.ProfilePictureUrl,
            })
            .ToListAsync();
        var targetIds = friends.Select(f => f.FollowedId).ToList();
        var relationshipStatuses = targetIds.Count > 0
        ? await GetRelationshipStatusesAsync(currentUserId, targetIds)
        : new Dictionary<int, RelationshipStatus>();

        return friends
        .Select(f => new FriendResponse(
            f.FollowedId,
            f.DisplayName,
            f.ProfilePictureUrl,
            relationshipStatuses.TryGetValue(f.FollowedId, out var status)
                ? status.ToString()
                : RelationshipStatus.Follow.ToString()))
        .ToList();
    }

    /// <summary>
    ///  Counts how many friends a user has.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>A count the given users friends.</returns>
    public async Task<int> GetFriendCountAsync(int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        return await context.Follow.AsNoTracking().Where(f =>
            f.FollowerId == userId &&
            context.Follow.Any(reverse =>
                reverse.FollowerId == f.FollowedId &&
                reverse.FollowedId == userId))
            .CountAsync();
    }
}
