using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

public class Comment
{
    [Key]
    public int Id { get; set; }

    public int TaskId { get; set; }

    public TaskItem Task { get; set; } = null!;

    public int AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public int? ParentCommentId { get; set; }

    public Comment? ParentComment { get; set; }

    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    public bool IsDeleted { get; set; }
}