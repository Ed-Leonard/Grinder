using Grinder.Api.Services;
using System.Text.Json.Serialization;

namespace Grinder.Api.Models.DTOs;

public record PublicProfileResponse(
    int Id,
    string DisplayName,
    int FriendCount,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    RelationshipStatus RelationshipStatus,
    string? SelectedBadgeName,
    string? SelectedBadgeIcon
);