using Grinder.Api.Models.Entities;
using System.Text.Json.Serialization;

namespace Grinder.Api.Models.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationType Type { get; set; }
    public int? EntityId { get; set; }
    public bool Read { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string? CommentContent { get; set; }
    public ReactionItemDto? Reaction { get; set; }

    public int MessageCount { get; set; } = 1;
    public string? MessagePreview { get; set; }
}

public class NotificationPageDto
{
    public List<NotificationDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
