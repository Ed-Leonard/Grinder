using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Represents a single message sent between two users. A "conversation" is
/// derived by querying messages where SenderId/ReceiverId match a pair of
/// users in either direction, rather than a separate Conversation entity.
/// </summary>
public class DirectMessage
{
	public int Id { get; set; }
	public int SenderId { get; set; }
	public int ReceiverId { get; set; }

	[MaxLength(256)]
	public string? Content { get; set; }
	public int? SharedTaskId { get; set; }
	public bool IsRead { get; set; }
	public DateTimeOffset SentAt { get; set; }

	public TaskItem? SharedTask { get; set; }
	public User Sender { get; set; } = null!;
	public User Receiver { get; set; } = null!;
}
