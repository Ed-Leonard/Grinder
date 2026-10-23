using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace Grinder.Api.Controllers;

/// <summary>
///  API controller responsible for managing follow relationships between users.
///  Provides endpoints for following, unfollowing, and checking follow status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FollowsController : ControllerBase
{
    private readonly IFollowService _followService;

    public FollowsController(IFollowService followService)
    {
        _followService = followService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    ///  Allows the authenticated user to follow another user by their user ID.
    ///  If the follow relationship already exists, a BadRequest response is returned.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>No content if successful; BadRequest if the follow relationship already exists.</returns>
    [HttpPost("{userId:int}")]
    public async Task<IActionResult> Follow(int userId)
    {
        try
        {
            await _followService.FollowAsync(CurrentUserId, userId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    ///   Returns relationship statuses for a set of users using the same shared logic as the explore page.
    /// </summary>
    /// <param name="userIds">The IDs of the users to check relationship status for.</param>
    [HttpGet("relationship-statuses")]
    public async Task<IActionResult> GetRelationshipStatuses([FromQuery(Name = "userIds")] IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Ok(new { relationships = new Dictionary<int, string>() });
        }

        var statuses = await _followService.GetRelationshipStatusesAsync(CurrentUserId, ids);
        return Ok(new { relationships = statuses.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()) });
    }

    /// <summary>
    ///   Allows the authenticated user to unfollow another user by their user ID.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>No content if successful.</returns>
    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Unfollow(int userId)
    {
        try
        {
            await _followService.UnfollowAsync(CurrentUserId, userId);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // the int route constraint on GET /api/follows{userId:int} means it won't conflict with /friends
    [HttpGet("friends/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<FriendResponse>>> GetFriends(int userId)
    {
        try
        {
            var friends = await _followService.GetFriendsAsync(userId, CurrentUserId);
            return Ok(friends);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }


    }
}
