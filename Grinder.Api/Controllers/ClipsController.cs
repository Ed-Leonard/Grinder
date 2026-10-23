using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using System.Security.Claims;

namespace Grinder.Api.Controllers;

/// <summary>
/// Serves the current user's clips feed
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ClipController : ControllerBase
{
    private readonly IClipService _clipService;
    
    public ClipController(IClipService clipService)
    {
        _clipService = clipService;
    }
    
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Returns a page of the current user's clips feed, newest first.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ClipPageDto>> GetClips([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var result = await _clipService.GetClipsAsync(CurrentUserId, page, limit);
        return Ok(result);
    }
}