public record ReactionItemDto(
    int Id,
    string Name,
    string FileName
);

public class ReactionResponse
{
    public required ReactionItemDto Reaction { get; set; }
    public int Count { get; set; }
    public bool ReactedByCurrentUser { get; set; }
}
