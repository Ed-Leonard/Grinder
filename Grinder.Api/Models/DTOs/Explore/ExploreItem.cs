namespace Grinder.Api.Models.DTOs;

public class ExploreItem
{
    public int Id { get; set; }
    public string Title { get; set; } = String.Empty;
    public List<AttachmentResponse> TaskAttachments { get; set; } = new();
}
