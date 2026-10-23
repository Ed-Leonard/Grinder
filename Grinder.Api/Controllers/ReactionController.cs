using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Utilities;

[ApiController]
[Route("api")]
[Authorize]
public class TaskReactionsController : ControllerBase
{
    private readonly IReactionService _reactionService;

    public TaskReactionsController(IReactionService reactionService)
    {
        _reactionService = reactionService;
    }

    /// <summary>
    /// Gets all available reactions.
    /// </summary>
    /// <returns>A list of all available reactions.</returns>
    /// <response code="200">The reactions were successfully retrieved.</response>
    [HttpGet("reactions")]
    public async Task<ActionResult<IEnumerable<Reaction>>> GetReactions()
    {
        var reactions = await _reactionService.GetReactionsAsync();
        return Ok(reactions);
    }

    /// <summary>
    /// Toggles a reaction on a task for the current user.
    /// </summary>
    /// <param name="taskId">The ID of the task to react to.</param>
    /// <param name="reactionId">The ID of the reaction to add or remove.</param>
    /// <returns>
    /// A response indicating the result of toggling the reaction.
    /// </returns>
    /// <response code="200">The reaction was successfully toggled.</response>
    /// <response code="401">The user is not authenticated.</response>
    [HttpPost("tasks/{taskId}/reactions/{reactionId}")]
    public async Task<ActionResult<ReactionResponse>> ToggleReaction(int taskId, int reactionId)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }
        try
        {
            var result = await _reactionService.ToggleReactionAsync(taskId, reactionId, userId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}