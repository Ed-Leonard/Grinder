using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Represents a livestream session backed by a unique LiveKit room
/// </summary>
public class LiveStream
{
    [Key]
    public int Id { get; set; }

    [MaxLength(128)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(64)]
    public string RoomName { get; set; } = string.Empty;

    public int TaskId { get; set; }

    [JsonIgnore]
    public TaskItem? Task { get; set; }

    public int UserId { get; set; }

    public User? User { get; set; }

    public TaskVisibility Visibility { get; set; } = TaskVisibility.Private;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset? EndedAt { get; set; }
}
