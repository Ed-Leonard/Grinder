using System.Text.Json.Serialization;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;

public record SearchResponseDto
{
    public required List<UserSearchResultDto> Users { get; init; }
    public required List<TaskResponse> Tasks { get; init; }
    public required int Page { get; init; }
    public required int TotalPages { get; init; }
}

public class UserSearchResultDto
{
    public required int Id { get; init; }
    public required string DisplayName { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required RelationshipStatus RelationshipStatus { get; init; }

    public static UserSearchResultDto FromEntity(User user, RelationshipStatus relationshipStatus)
    {
        return new UserSearchResultDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            RelationshipStatus = relationshipStatus,
        };
    }
}
