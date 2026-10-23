namespace Grinder.Api.Models.DTOs;

public class ExplorePage
{
    public List<ExploreItem> Items { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
}
