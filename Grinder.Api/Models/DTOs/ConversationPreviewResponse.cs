namespace Grinder.Api.Models.DTOs;

/// <summary>
/// One row is the auth'd user's DM inbox, UnreadCount is messages sent TO this user that they haven't read
/// </summary>
public record ConversationPreviewResponse(
    int OtherUserId,
    string OtherUserDisplayName,
    string LastMessagePreview,
    int LastMessageSenderId,
    DateTimeOffset LastMessageSentAt,
    int UnreadCount
)
{
    public bool HasUnread => UnreadCount > 0;
};
