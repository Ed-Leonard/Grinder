namespace Grinder.Api.Models.DTOs;

/// <summary>
/// Response DTO for a direct message, used to send message details 
/// back to the client after a message is sent or when retrieving conversation history.
/// </summary>
/// <param name="Id">The ID of the message.</param>
/// <param name="SenderId">The ID of the user sending the message.</param>
/// <param name="ReceiverId">The ID of the user receiving the message.</param>
/// <param name="Content">The content of the message.</param>
/// <param name="SentAt">The timestamp when the message was sent.</param>
public record DirectMessageResponse(
	int Id,
	int SenderId,
	int ReceiverId,
	string? Content,
	int? SharedTaskId,
	SharedTaskPreview? SharedTask,
	bool Read,
	DateTimeOffset SentAt
);

public record ConversationPageResponse(List<DirectMessageResponse> Messages, int Page, bool HasMore);

public class SharedTaskPreview
{
	public int Id { get; set; }
	public int UserId { get; set; }
	public string UserDisplayName { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public bool IsClip { get; set; }
	public AttachmentResponse? VideoAttachment { get; set; }
	public AttachmentResponse? ImageAttachment { get; set; }
}

