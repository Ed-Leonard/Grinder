using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;

namespace Grinder.Api.Services;

public interface ICommentService
{
    Task<CommentResponse> CreateCommentAsync(int userId, CreateCommentRequest request);
    Task<DeleteCommentResult> DeleteCommentAsync(int userId, int commentId);
    Task<TaskCommentsResponse> GetCommentsByTaskIdAsync(int userId, int taskId);
    Task<PaginatedRepliesResponse> GetRepliesByCommentIdAsync(int userId, int commentId, int skip = 0, int take = 10);
}

public class CommentService : ICommentService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly INotificationService _notificationService;

    public CommentService(IDbContextFactory<DatabaseContext> dbContextFactory, INotificationService notificationService)
    {
        _dbContextFactory = dbContextFactory;
        _notificationService = notificationService;
    }

    private static async Task<bool> HasTaskAccessAsync(DatabaseContext context, TaskItem task, int userId)
    {
        if (task.UserId == userId || task.Visibility == TaskVisibility.Public)
        {
            return true;
        }

        return task.Visibility == TaskVisibility.FriendsOnly &&
            await context.Follow.AnyAsync(f =>
                f.FollowerId == userId &&
                f.FollowedId == task.UserId &&
                context.Follow.Any(reverse =>
                    reverse.FollowerId == task.UserId &&
                    reverse.FollowedId == userId));
    }

    /// <summary>
    ///  Creates a new comment for a specific task, optionally as a reply to an existing comment.
    /// </summary>
    /// <param name="userId">The ID of the user creating the comment.</param>
    /// <param name="request">The request containing comment details.</param>
    /// <returns>The created comment response.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<CommentResponse> CreateCommentAsync(int userId, CreateCommentRequest request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var task = await context.TaskItems
            .FirstOrDefaultAsync(t => t.Id == request.TaskId)
            ?? throw new KeyNotFoundException("Task not found.");

        if (!await HasTaskAccessAsync(context, task, userId))
        {
            throw new UnauthorizedAccessException("You do not have access to this task.");
        }

        if (request.ParentCommentId.HasValue)
        {
            var parent = await context.Comments
                .FindAsync(request.ParentCommentId.Value)
                ?? throw new KeyNotFoundException("The comment you're replying to no longer exists. Please refresh the page and try again.");

            if (parent.TaskId != request.TaskId)
                throw new InvalidOperationException("Comment and parent must belong to the same task.");
        }

        var comment = new Comment
        {
            TaskId = request.TaskId,
            AuthorId = userId,
            Content = request.Content,
            CreatedAt = DateTimeOffset.UtcNow,
            ParentCommentId = request.ParentCommentId
        };

        context.Comments.Add(comment);
        await context.SaveChangesAsync();

        await context.Entry(comment).Reference(c => c.Author).LoadAsync();

        // Notify the task owner when someone comments on their task
        if (!request.ParentCommentId.HasValue)
        {
            await _notificationService.CreateAsync(task.UserId, userId, NotificationType.Comment, task.Id, commentId: comment.Id, commentContent: comment.Content);
        }
        else
        {
            // Notify the parent comment's author when someone replies to their comment
            var parentComment = await context.Comments.FindAsync(request.ParentCommentId.Value);
            if (parentComment != null)
            {
                await _notificationService.CreateAsync(parentComment.AuthorId, userId, NotificationType.Comment, task.Id, commentId: comment.Id, commentContent: comment.Content);
            }
        }

        return CommentResponse.FromEntity(comment);
    }

    /// <summary>
    /// Deletes a comment by its ID. Only the author of the comment can delete the comment. 
    /// If the comment has replies, it will be marked as deleted instead of being removed from the database.
    /// </summary>
    /// <param name="userId">The ID of the user attempting to delete the comment.</param>
    /// <param name="commentId">The ID of the comment to be deleted.</param>
    /// <returns>The result of the delete operation, including whether the comment was removed or marked as deleted.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<DeleteCommentResult> DeleteCommentAsync(int userId, int commentId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var comment = await context.Comments
            .Include(c => c.Task)
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
        {
            return new DeleteCommentResult
            {
                Removed = true,
            };
        }

        if (comment.AuthorId != userId && comment.Task.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this comment.");
        }

        var hasReplies = await context.Comments.AnyAsync(c => c.ParentCommentId == commentId);

        var notificationsForDeletedComment = await context.Notification
            .Where(n => n.CommentId == commentId)
            .ToListAsync();
        context.Notification.RemoveRange(notificationsForDeletedComment);

        if (!hasReplies)
        {
            context.Comments.Remove(comment); // Hard delete if no replies exist
            await context.SaveChangesAsync();
            return new DeleteCommentResult
            {
                Removed = true,
            };
        }

        comment.IsDeleted = true;
        comment.Content = string.Empty; // Soft delete by clearing the content


        

        await context.SaveChangesAsync();

        var response = CommentResponse.FromEntity(comment);
        response.ReplyCount = await context.Comments.CountAsync(c => c.ParentCommentId == commentId);

        return new DeleteCommentResult
        {
            Removed = false,
            Comment = response
        };
    }

    /// <summary>
    ///  Retrieves all comments for a specific task, including nested replies, and returns them in a hierarchical structure.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the comments.</param>
    /// <param name="taskId">The ID of the task for which to retrieve comments.</param>
    /// <returns>A response containing the top-level comments and the total comment count for the task.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    public async Task<TaskCommentsResponse> GetCommentsByTaskIdAsync(int userId, int taskId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var task = await context.TaskItems
            .FirstOrDefaultAsync(t => t.Id == taskId)
            ?? throw new KeyNotFoundException("Task not found.");

        if (!await HasTaskAccessAsync(context, task, userId))
        {
            throw new UnauthorizedAccessException("You do not have access to this task.");
        }

        var topLevelComments = await context.Comments
            .Include(c => c.Author)
            .Where(c => c.TaskId == taskId && c.ParentCommentId == null)
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        var replyCounts = (await context.Comments
            .Where(c => c.TaskId == taskId && c.ParentCommentId != null)
            .GroupBy(c => c.ParentCommentId)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToListAsync())
            .ToDictionary(x => x.ParentId!.Value, x => x.Count);

        var responses = topLevelComments.Select(c =>
        {
            var response = CommentResponse.FromEntity(c);
            response.ReplyCount = replyCounts.GetValueOrDefault(c.Id, 0);
            return response;
        }).ToList();

        var totalCount = await context.Comments.CountAsync(c => c.TaskId == taskId && !c.IsDeleted);

        return new TaskCommentsResponse
        {
            Comments = responses,
            TotalCount = totalCount
        };
    }

    /// <summary>
    ///  Retrieves paginated direct replies for a specific comment.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the replies.</param>
    /// <param name="commentId">The ID of the parent comment.</param>
    /// <param name="skip">Number of replies to skip.</param>
    /// <param name="take">Number of replies to return.</param>
    /// <returns>A paginated response of reply comments.</returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="UnauthorizedAccessException"></exception>
    public async Task<PaginatedRepliesResponse> GetRepliesByCommentIdAsync(int userId, int commentId, int skip = 0, int take = 10)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var comment = await context.Comments
        .Include(c => c.Task)
        .FirstOrDefaultAsync(c => c.Id == commentId)
        ?? throw new KeyNotFoundException("Comment not found.");

        if (!await HasTaskAccessAsync(context, comment.Task, userId))
        {
            throw new UnauthorizedAccessException("You do not have access to this comment's task.");
        }

        var query = context.Comments
            .Include(c => c.Author)
            .Where(c => c.ParentCommentId == commentId)
            .OrderBy(c => c.Id);

        var totalCount = await query.CountAsync();

        var replies = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var replyIds = replies.Select(r => r.Id).ToList();
        var replyCounts = await context.Comments
            .Where(c => c.ParentCommentId != null && replyIds.Contains(c.ParentCommentId.Value))
            .GroupBy(c => c.ParentCommentId)
            .Select(g => new { ParentId = g.Key!.Value, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId, x => x.Count);

        var replyResponses = replies.Select(r =>
        {
            var response = CommentResponse.FromEntity(r);
            response.ReplyCount = replyCounts.GetValueOrDefault(r.Id, 0);
            return response;
        }).ToList();

        return new PaginatedRepliesResponse
        {
            Replies = replyResponses,
            TotalCount = totalCount,
            HasMore = skip + take < totalCount
        };
    }
}