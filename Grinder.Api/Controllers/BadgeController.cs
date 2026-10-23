using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Grinder.Api.Services;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;

namespace Grinder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BadgeController : ControllerBase
{
    private readonly IBadgeService _badgeService;

    public BadgeController(IBadgeService badgeService)
    {
        _badgeService = badgeService;
    }

    /// <summary>
    /// Retrieves all badges and their earned status for the authenticated user.
    /// Each badge will indicate whether it has been unlocked by the user and, if so, 
    /// when it was earned.
    /// </summary>
    /// <returns>
    /// A list of badges with their earned status.
    /// </returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BadgeResponse>>> GetBadges()
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }
        await _badgeService.EvaluateBadgesAsync(userId.Value);
        var badges = await _badgeService.GetAllBadgesForUserAsync(userId.Value);
        return Ok(badges);
    }

    /// <summary>
    /// Allows the authenticated user to select a badge to display on their profile.
    /// The user can only select a badge that they have already earned.
    /// </summary>
    /// <param name="request">The request containing the badge ID to select.</param>
    /// <returns>HTTP response indicating the result of the operation.</returns>
    [HttpPut("select")]
    public async Task<ActionResult> SelectBadge([FromBody] SelectBadgeRequest request)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null) return Unauthorized();
 
        var success = await _badgeService.SelectBadgeAsync(userId.Value, request.BadgeId);
        if (!success) return BadRequest("Badge not earned or not found.");
 
        return Ok();
    }
}