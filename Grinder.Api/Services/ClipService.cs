using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Services;

public interface IClipService
{
    Task<ClipPageDto> GetClipsAsync(int userId, int page, int limit);
}

/// <summary>
/// Responsible for serving the reverse-chronological clips feed: public clips from anyone,
/// plus friends-only clips from the viewer's mutual friends.
/// </summary>
public class ClipService : IClipService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IReactionService _reactionService;

    public ClipService(IDbContextFactory<DatabaseContext> dbContextFactory, IReactionService reactionService)
    {
        _dbContextFactory = dbContextFactory;
        _reactionService = reactionService;
    }

    /// <summary>
    /// Returns a page of clips visible to the given user (public, or friends-only from a mutual friend), newest first.
    /// </summary>
    public async Task<ClipPageDto> GetClipsAsync(int userId, int page, int limit)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        limit = Math.Clamp(limit, 1, 50);
        page = Math.Max(page, 1);

        var friendIds = await context.Follow
            .AsNoTracking()
            .Where(f => f.FollowerId == userId &&
                        context.Follow.Any(reverse =>
                            reverse.FollowerId == f.FollowedId &&
                            reverse.FollowedId == userId))
            .Select(f => f.FollowedId)
            .ToListAsync();

        var query = context.TaskItems
            .AsNoTracking()
            .Where(t => t.IsClip
                        && t.ParentTaskId == null
                        && t.UserId != userId
                        && (t.Visibility == TaskVisibility.Public
                            || (t.Visibility == TaskVisibility.FriendsOnly && friendIds.Contains(t.UserId))));

        var totalCount = await query.CountAsync();

        var tasks = await query
            .OrderByDescending(t => t.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Include(t => t.User)
            .ThenInclude(t => t.SelectedBadge)
            .Include(t => t.TaskAttachments)
            .Include(t => t.Reactions)
                .ThenInclude(r => r.Reaction)
            .ToListAsync();

        var items = tasks.Select(t => new ClipItemDto
        {
            Id = t.Id,
            UserId = t.UserId,
            UserDisplayName = t.User!.DisplayName,
            UserProfilePictureUrl = t.User.ProfilePictureUrl,
            Title = t.Title,
            Description = t.Description,
            CreatedAt = t.ClipCreatedAt ?? t.CreatedAt,
            TaskAttachments = t.TaskAttachments
                .Select(AttachmentResponse.FromEntity)
                .ToList(),
            SelectedBadgeIcon = t.User.SelectedBadge?.Icon,
            SelectedBadgeName = t.User.SelectedBadge?.Name,
            Reactions = _reactionService.GetReactionsForTaskAsync(t.Reactions, userId).Result,
        }).ToList();

        return new ClipPageDto
        {
            Items = items,
            Page = page,
            TotalPages = (int)Math.Ceiling(totalCount / (double)limit),
        };
    }
}
