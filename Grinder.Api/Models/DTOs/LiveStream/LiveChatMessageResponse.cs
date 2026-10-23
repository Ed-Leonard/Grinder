namespace Grinder.Api.Models.DTOs;

public record LiveChatMessageResponse(
    int Id,
    string RoomName,
    int SenderId,
    string SenderName,
    string Content,
    DateTimeOffset SentAt
);

public record LiveChatPageResponse(List<LiveChatMessageResponse> Messages, int Page, bool HasMore);

