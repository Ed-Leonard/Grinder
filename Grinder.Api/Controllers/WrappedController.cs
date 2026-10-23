namespace Grinder.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;

[ApiController]
[Route("api/wrapped")]
[Authorize]
public class WrappedController : ControllerBase
{
    private readonly IWrappedService _wrappedService;
    private readonly TimeProvider _timeProvider;

    public WrappedController(IWrappedService wrappedService, TimeProvider timeProvider)
    {
        _wrappedService = wrappedService;
        _timeProvider = timeProvider;
    }

    [HttpGet("eligibility")]
    public async Task<ActionResult<WrappedEligibilityResponse>> GetEligibility()
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        var mostRecentEligibleYear = await _wrappedService.GetMostRecentEligibleYearAsync(userId.Value);
        var currentEligibleYear = await _wrappedService.GetCurrentEligibleYearAsync(userId.Value);
        var isEligible = currentEligibleYear != null;
        var dismissalYear = currentEligibleYear ?? _timeProvider.GetLocalNow().Year;
        var dismissed = await _wrappedService.IsDismissedAsync(userId.Value, dismissalYear);

        return Ok(new WrappedEligibilityResponse(isEligible, mostRecentEligibleYear, currentEligibleYear, dismissed));
    }

    [HttpPost("dismiss")]
    public async Task<IActionResult> Dismiss([FromBody] DismissWrappedRequest request)
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        await _wrappedService.DismissAsync(userId.Value, request.Year);
        return NoContent();
    }


    [HttpGet("constant-eligibility")]
    public async Task<ActionResult<WrappedConstantEligibilityResponse>> GetGenerateEligibility()
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null) return Unauthorized();

        var now = _timeProvider.GetUtcNow();
        var currentEligible = await _wrappedService.IsEligibleForConstantCurrentYearAsync(userId.Value);
        var previousEligible = await _wrappedService.IsEligibleForConstantPreviousYearAsync(userId.Value);

        return Ok(new WrappedConstantEligibilityResponse(
            currentEligible, now.Year,
            previousEligible, now.Year - 1));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<WrappedStatsResponse>> GetStats([FromQuery] int year)
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null) return Unauthorized();

        var now = _timeProvider.GetUtcNow();
        bool eligible;
        if (year == now.Year)
        {
            eligible = await _wrappedService.IsEligibleForConstantCurrentYearAsync(userId.Value);
        }
        else if (year == now.Year - 1)
        {
            eligible = await _wrappedService.IsEligibleForConstantPreviousYearAsync(userId.Value);
        }
        else
        {
            return NotFound();
        }

        if (!eligible) return NotFound();

        var stats = await _wrappedService.GetWrappedStatsAsync(userId.Value, year);
        return Ok(stats);
    }
}
