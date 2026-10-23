using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Represents the different stages a task can be in
/// </summary>
public enum TaskItemStatus
{
    Todo,
    InProgress,
    Done
}

public enum TaskVisibility
{
    Private,
    FriendsOnly,
    Public
}

public enum TaskField
{
    Title,
    Description,
    Start,
    Due,
    Status,
    Categories,
    ParentTask
}

/// <summary>
/// Represents a task and its state
/// </summary>
public class TaskItem
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int? ParentTaskId { get; set; }
    public TaskItem? ParentTask { get; set; }

    public ICollection<TaskItem> Children { get; set; } = new List<TaskItem>();

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Description { get; set; }

    // needs service layer validation to detect past dates
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// When this task was last marked Done. Null if it is not currently Done.
    /// Distinct from UpdatedAt, which also changes on unrelated edits.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public DateOnly DueDate { get; set; }
    public TimeOnly? DueTime { get; set; }

    public List<TaskField> ChangedFields { get; set; } = new();

    public TaskItemStatus ItemStatus { get; set; }

    public TaskVisibility Visibility { get; set; } = TaskVisibility.Private;

    public int Depth { get; set; }

    public int Order { get; set; }

    [JsonIgnore]

    public ICollection<Category> Categories { get; set; } = new List<Category>();

    public bool IsClip { get; set; } = false;

    /// <summary>
    /// When this task became a clip (i.e. gained its single video attachment). Null if it has never been a clip.
    /// Distinct from CreatedAt, which is set once when the task itself is first created and may predate this.
    /// </summary>
    public DateTimeOffset? ClipCreatedAt { get; set; }

    public List<TaskAttachment> TaskAttachments { get; set; } = new();

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public ICollection<TaskReaction> Reactions { get; set; } = new List<TaskReaction>();
}
