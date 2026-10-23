
/// <summary>
/// Represents a request to create a LiveKit access token for a livestream.
/// </summary>
/// <param name="Title"> The title of the livestream. </param>
/// <param name="TaskId"> The ID of the task associated with the livestream. </param>
/// <param name="Visibility"> The visibility of the livestream. </param>
public record TokenRequest(string Title, int TaskId, int Visibility);

/// <summary>
/// Represents a response containing a LiveKit access token and related information for a livestream.
/// </summary>
/// <param name="Url"> The URL of the LiveKit server. </param>
/// <param name="Token"> The access token for the livestream. </param>
/// <param name="RoomName"> The name of the room for the livestream. </param>
public record TokenResponse(string Url, string Token, string RoomName);

public abstract record CreateTokenResult
{
    public sealed record Success(string Url, string Token, string RoomName) : CreateTokenResult;
    public sealed record ValidationFailed(string Error) : CreateTokenResult;
    public sealed record ConfigMissing(string Error) : CreateTokenResult;
    
    public sealed record AlreadyLive : CreateTokenResult;
}

public abstract record JoinTokenResult
{
    public sealed record Success(string Url, string Token) : JoinTokenResult;
    public sealed record NotFound : JoinTokenResult;
    public sealed record ConfigMissing(string Error) : JoinTokenResult;

}