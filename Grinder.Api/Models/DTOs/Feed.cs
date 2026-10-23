using Grinder.Api.Models.Entities;

namespace Grinder.Api.Models.DTOs;

public class FeedItemDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsUpdated { get; set; }
    public List<TaskField>? ChangedFields { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public DateOnly? DueDate { get; set; }
    public TimeOnly? DueTime { get; set; }
    
    public string? SelectedBadgeName { get; set; }
    public string? SelectedBadgeIcon { get; set; }

    public TaskItemStatus ItemStatus { get; set; }
    public List<AttachmentResponse> TaskAttachments { get; set; } = new();
    public List<TaskCategoryResponse> Categories { get; set; } = new();
    public List<ReactionResponse> Reactions { get; set; } = new();
}

public class FeedPageDto
{
    public List<FeedItemDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
