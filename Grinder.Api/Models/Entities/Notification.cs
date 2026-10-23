using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Represents the type of notification that can be sent to a user.
/// </summary>
public enum NotificationType
{
    Follow,
    Comment,
    React,
    Message,
}

/// <summary>
///  Represents a notification sent to a user, containing information about the sender, type, and related entity.
/// </summary>
public class Notification
{
    [Key]
    public int Id { get; set; }
    public int RecipientId { get; set; }
    public int SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public NotificationType Type { get; set; }
    public int? EntityId { get; set; }   // null for Follow
    public bool Read { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public int? ReactionId { get; set; }
    public Reaction? Reaction { get; set; }

    // Message notifications are bumped rather than duplicated; these track the running count and latest content.
    public int MessageCount { get; set; } = 1;
    public string? MessagePreview { get; set; }
}
