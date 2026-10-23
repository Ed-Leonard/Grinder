namespace Grinder.Api.Models.DTOs;

public record FriendResponse(
    int Id,
    string DisplayName,
    string? ProfilePictureUrl,
    string RelationshipStatus
);
