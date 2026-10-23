namespace Grinder.Api.Models.Entities;

public class TaskView
{
    public int UserId { get; set; }
    public int TaskItemId { get; set; }

    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public TaskItem TaskItem { get; set; } = null!;
}
