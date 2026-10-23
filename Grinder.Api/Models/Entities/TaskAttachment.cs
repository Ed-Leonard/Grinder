namespace Grinder.Api.Models.Entities;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class TaskAttachment
{
    [Key]
    public int Id { get; set; }

    // Null until the task is created or updated
    public int? TaskItemId { get; set; }

    [JsonIgnore]
    public TaskItem? Task { get; set; }

    [JsonIgnore]
    public int UserId { get; set; }

    [JsonIgnore]
    [MaxLength(255)]
    public string StoredFileName { get; set; } = String.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = String.Empty;

    public long FileSize { get; set; }

    [MaxLength(500)]
    public string Url { get; set; } = String.Empty;

    public int? width { get; set; }
    public int? height { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
