using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

public class Reaction
{
    [Key]
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string Name { get; set; }
}
public class TaskReaction
{
    [Key]
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public int ReactionId { get; set; }
    public Reaction? Reaction { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
