using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using System.Security.Claims;

namespace Grinder.Api.Controllers;

/// <summary>
/// Fetches content for the explore page
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ExploreController : ControllerBase
{
    private readonly IExploreService _exploreService;

    public ExploreController(IExploreService exploreService)
    {
        _exploreService = exploreService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns the default explore feed of public tasks, those trending are ranked higher 
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ExplorePage>> GetExploreFeed([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var result = await _exploreService.GetExploreFeedAsync(CurrentUserId, page, limit);
        return Ok(result);
    }
}

