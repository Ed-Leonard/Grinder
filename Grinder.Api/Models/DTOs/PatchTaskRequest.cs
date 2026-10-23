namespace Grinder.Api.Models.DTOs;

using Grinder.Api.Models.Entities;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request model used when editing an existing Task.
/// </summary>
public class PatchTaskRequest
{
    public int? ParentTaskId { get; set; }

    [StringLength(128, MinimumLength = 3,
        ErrorMessage = "Title is required and must be between 3 and 128 characters long")]
    public string? Title { get; set; }

    [StringLength(2048,
        ErrorMessage = "Description must be 2048 characters or less")]
    public string? Description { get; set; }

    public string? StartDate { get; set; }

    public string? StartTime { get; set; }

    public string? DueDate { get; set; }

    public string? DueTime { get; set; }

    [EnumDataType(typeof(TaskItemStatus))]
    public TaskItemStatus? ItemStatus { get; set; }

    [EnumDataType(typeof(TaskVisibility))]
    public TaskVisibility? Visibility { get; set; }

    public List<int>? CategoryIds { get; set; }

    public List<int>? AttachmentIds { get; set; }
}
