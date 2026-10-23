using Grinder.Api.Models.Entities;
namespace Grinder.Api.Models.DTOs;

public class CommentAuthorResponse
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ProfilePictureUrl { get; set; } = string.Empty;
}

public class CommentResponse
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int AuthorId { get; set; }

    public CommentAuthorResponse Author { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public int? ParentCommentId { get; set; }
    public bool IsDeleted { get; set; }

    public int ReplyCount { get; set; }

    public List<CommentResponse> Replies { get; set; } = [];

    public static CommentResponse FromEntity(Comment comment)
    {
        return new CommentResponse
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            AuthorId = comment.AuthorId,

            Author = new CommentAuthorResponse
            {
                Id = comment.Author.Id,
                DisplayName = comment.Author.DisplayName,
                ProfilePictureUrl = comment.Author.ProfilePictureUrl ?? string.Empty
            },

            Content = comment.IsDeleted ? "Deleted comment" : comment.Content,
            CreatedAt = comment.CreatedAt,
            ParentCommentId = comment.ParentCommentId,
            IsDeleted = comment.IsDeleted
        };
    }
}

public class PaginatedRepliesResponse
{
    public List<CommentResponse> Replies { get; set; } = [];
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
}

public class TaskCommentsResponse
{
    public List<CommentResponse> Comments { get; set; } = [];
    public int TotalCount { get; set; }
}

public class DeleteCommentResult
{
    public bool Removed { get; set; }
    public CommentResponse? Comment { get; set; }
}