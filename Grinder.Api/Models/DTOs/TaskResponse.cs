using Grinder.Api.Models.Entities;

public class TaskCategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public static TaskCategoryResponse FromEntity(Category category)
    {
        return new TaskCategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
        };
    }
}

public class TaskResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public DateOnly? DueDate { get; set; }
    public TimeOnly? DueTime { get; set; }
    public TaskItemStatus ItemStatus { get; set; }
    public TaskVisibility Visibility { get; set; }
    public int? ParentTaskId { get; set; }
    public int Order { get; set; }
    public List<TaskCategoryResponse> Categories { get; set; } = new();
    public List<TaskResponse> Children { get; set; } = new();
    public int Depth { get; set; }
    public bool IsClip { get; set; }
    public List<AttachmentResponse> TaskAttachments { get; set; } = new();
    public List<ReactionResponse> Reactions { get; set; } = new();

    public static TaskResponse FromEntity(TaskItem task, int currentUserId)
    {
        return new TaskResponse
        {
            Id = task.Id,
            UserId = task.UserId,
            Title = task.Title,
            Description = task.Description,
            CreatedAt = task.CreatedAt,
            StartDate = task.StartDate,
            StartTime = task.StartTime,
            DueDate = task.DueDate,
            DueTime = task.DueTime,
            ItemStatus = task.ItemStatus,
            Visibility = task.Visibility,
            ParentTaskId = task.ParentTaskId,
            Order = task.Order,
            Depth = task.Depth,
            IsClip = task.IsClip,
            Categories = task.Categories?.Select(TaskCategoryResponse.FromEntity).ToList()
                ?? new List<TaskCategoryResponse>(),
            Children = task.Children?.Select(c => FromEntity(c, currentUserId)).ToList()
                ?? new List<TaskResponse>(),
            TaskAttachments = task.TaskAttachments?.Select(AttachmentResponse.FromEntity).ToList()
                ?? new List<AttachmentResponse>(),
            Reactions = task.Reactions?
                .GroupBy(r => r.ReactionId)
                .Select(g => new ReactionResponse
                {
                    Reaction = new ReactionItemDto(g.First().Reaction!.Id, g.First().Reaction!.Name, g.First().Reaction!.FileName),
                    Count = g.Count(),
                    ReactedByCurrentUser = g.Any(r => r.UserId == currentUserId),
                })
                .ToList() ?? new List<ReactionResponse>(),
        };
    }
}