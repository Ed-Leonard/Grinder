namespace Grinder.Api.Models.DTOs;

public class ClipItemDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public string? UserProfilePictureUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<AttachmentResponse> TaskAttachments { get; set; } = new();

    public string? SelectedBadgeName { get; set; }

    public string? SelectedBadgeIcon { get; set; }

    public List<ReactionResponse> Reactions { get; set; } = new();
}

public class ClipPageDto
{
    public List<ClipItemDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
