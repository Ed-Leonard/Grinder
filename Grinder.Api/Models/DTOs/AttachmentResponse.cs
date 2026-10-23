using Grinder.Api.Models.Entities;

public class AttachmentResponse
{
    public int Id { get; set; }
    public int? TaskItemId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Url { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static AttachmentResponse FromEntity(TaskAttachment attachment)
    {
        return new AttachmentResponse
        {
            Id = attachment.Id,
            TaskItemId = attachment.TaskItemId,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            Url = attachment.Url,
            Width = attachment.width,
            Height = attachment.height,
            CreatedAt = attachment.CreatedAt
        };
    }
}
