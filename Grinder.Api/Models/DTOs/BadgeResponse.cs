namespace Grinder.Api.Models.DTOs;

public class BadgeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public DateTime? EarnedAt { get; set; }
    public int Progress { get; set; }
    public int Threshold { get; set; }
}