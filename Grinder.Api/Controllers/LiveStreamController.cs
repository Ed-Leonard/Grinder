using System.Security.Claims;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Grinder.Api.Controllers;

/// <summary>
/// Controller for managing live streams. Provides endpoints to retrieve currently active live streams that the current user is permitted to view.
/// </summary>
[ApiController]
[Authorize]
[Route("api/livestreams")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LiveStreamController : ControllerBase
{
    private readonly ILiveStreamService _liveStreamService;
    private readonly ILiveStreamChatService _liveStreamChatService;

    public LiveStreamController(ILiveStreamService liveStreamService,  ILiveStreamChatService liveStreamChatService)
    {
        _liveStreamService = liveStreamService;
        _liveStreamChatService = liveStreamChatService;
    }

    /// <summary>
    /// Gets the ID of the currently authenticated user.
    /// </summary>
    /// <returns> The ID of the currently authenticated user.</returns>
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Creates a LiveKit access token for the current user to join a livestream room.
    /// Livekit access tokens are used to authenticate users with the LiveKit server and are valid for a short duration (30 minutes).
    /// </summary>
    /// <param name="request"> The request containing the livestream details. </param>
    /// <returns> The response containing the LiveKit access token and related information. </returns>
    [HttpPost("token")]
    public async Task<ActionResult<TokenResponse>> CreateToken([FromBody] TokenRequest request)
    {
        var identity = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(identity))
        {
            return Unauthorized();
        }

        var result = await _liveStreamService.CreateTokenAsync(identity, request);

        return result switch
        {
            CreateTokenResult.Success s => Ok(new TokenResponse(s.Url, s.Token, s.RoomName)),
            CreateTokenResult.ValidationFailed v => BadRequest(v.Error),
            CreateTokenResult.ConfigMissing c => Problem(c.Error, statusCode: StatusCodes.Status503ServiceUnavailable),
            CreateTokenResult.AlreadyLive => Conflict("You already have an active livestream."),
            _ => Problem("Unexpected error.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Returns currently active livestreams the current user is permitted to view, ranked with
    /// friends' streams first, then public streams from followed users, then everyone else,
    /// each group sorted by viewer count descending then most recently started.
    /// </summary>
    /// <param name="limit"> The number of results to return per page.</param>
    /// <param name="exclude"> A list of room names to exclude from the results.</param>
    /// <returns> A list of LiveStreamPageResponse objects representing the active livestreams.</returns>
    [HttpGet("active")]
    public async Task<ActionResult<LiveStreamPageResponse>> GetActive([FromQuery] int limit = 20, [FromQuery] string[]? exclude = null)
    {
        var streams = await _liveStreamService.GetActiveLiveStreamsAsync(CurrentUserId, limit, exclude ?? []);
        return Ok(streams);
    }

    /// <summary>
    /// Creates a subscribe-only LiveKit access token so the current user can watch a livestream.
    /// </summary>
    /// <param name="roomName"> The LiveKit room name of the livestream, as returned when the stream was started. </param>
    /// <returns>
    /// 200 with the LiveKit server URL, viewer token and room name if the user may watch the stream.
    /// 401 if the user's identity can't be read from their session.
    /// 404 if the room doesn't exist or the user isn't allowed to watch it. These are deliberately
    /// indistinguishable so that the existence of a private or friends-only room is not revealed.
    /// 503 if the LiveKit server configuration is missing.
    /// </returns>
    [HttpPost("join/{roomName}")]
    public async Task<ActionResult<TokenResponse>> JoinLive(string roomName)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var viewerId))
        {
            return Unauthorized();
        }

        var result = await _liveStreamService.CreateViewerTokenAsync(viewerId, roomName);

        return result switch
        {
            JoinTokenResult.Success s => Ok(new TokenResponse(s.Url, s.Token, roomName)),
            JoinTokenResult.NotFound => NotFound(),
            JoinTokenResult.ConfigMissing c => Problem(c.Error, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected error.", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Returns one page of persisted chat for a livestream, oldest message first.
    /// </summary>
    /// <param name="roomName"> The LiveKit room name of the livestream. </param>
    /// <param name="page"> The page number, starting at 1. </param>
    /// <param name="limit"> The number of messages per page. The service clamps this to between 1 and 100. </param>
    /// <returns>
    /// 200 with the page of messages.
    /// 404 if the room does not exist or the user is not allowed to watch it. These are deliberately
    /// indistinguishable so that the existence of a private or friends-only room is not revealed.
    /// </returns>
    [HttpGet("{roomName}/messages")]
    public async Task<ActionResult<LiveChatPageResponse>> GetMessages(string roomName, [FromQuery] int page = 1,
        [FromQuery] int limit = 20)
    {
        var result = await _liveStreamChatService.GetAsync(CurrentUserId, roomName, page, limit);

        if  (result is null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// Ends the current user's livestream by deleting its LiveKit room.
    /// </summary>
    /// <param name="roomName">The room name of the livestream to end.</param>
    /// <returns>204 on success; 404 when the room does not exist or belongs to someone else.</returns>
    [HttpDelete("{roomName}")]
    public async Task<IActionResult> EndLive(string roomName)
    {
        var ended = await _liveStreamService.EndLiveStreamAsync(CurrentUserId, roomName);
        return ended ? NoContent() : NotFound();
    }
}
