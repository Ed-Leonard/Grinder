using Grinder.Api.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.DTOs;

/// <summary>
/// Request model used when creating a new Task.
/// </summary>
public class CreateTaskRequest
{
    public int? ParentTaskId { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 3,
    ErrorMessage = "Title is required and must be between 3 and 128 characters long")]
    public string Title { get; set; } = null!;

    [StringLength(2048,
    ErrorMessage = "Description must be 2048 characters or less")]
    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public DateOnly DueDate { get; set; }

    public TimeOnly? DueTime { get; set; }

    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus ItemStatus { get; set; }

    [EnumDataType(typeof(TaskVisibility))]
    public TaskVisibility Visibility { get; set; } = TaskVisibility.Private;

    public List<int> CategoryIds { get; set; } = new List<int>();

    public List<int> AttachmentIds { get; set; } = new();
}
