using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using System.Security.Claims;

namespace Grinder.Api.Controllers;

/// <summary>
/// Serves the current user's friends feed.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns a page of the current user's friends feed, newest first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<FeedPageDto>> GetFeed([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var result = await _feedService.GetFriendsFeedAsync(CurrentUserId, page, limit);
        return Ok(result);
    }

    [HttpPost("viewed")]
    [Authorize]
    public async Task<IActionResult> MarkTasksAsViewed([FromBody] MarkTasksViewedRequest request)
    {
        if (request.TaskItemIds == null || request.TaskItemIds.Count == 0)
        {
            return BadRequest("At least one task ID is required.");
        }

        if (request.TaskItemIds.Count > 100)
        {
            return BadRequest("Too many task IDs in a single request.");
        }


        await _feedService.MarkTasksAsViewedAsync(CurrentUserId, request.TaskItemIds);
        return NoContent();
    }
}
