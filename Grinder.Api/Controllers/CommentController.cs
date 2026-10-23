using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Services;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;

namespace Grinder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService)
    {
        _commentService = commentService;
    }

    /// <summary>
    ///  Creates a new comment for a specific task, optionally as a reply to an existing comment.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<CommentResponse>> CreateComment([FromBody] CreateCommentRequest request)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var comment = await _commentService.CreateCommentAsync(userId.Value, request);
            return StatusCode(StatusCodes.Status201Created, comment);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? "Request", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("Request", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    ///  Deletes a comment by its ID. Only the author of the comment.
    /// </summary>
    /// <param name="commentId"></param>
    /// <returns></returns>
    [HttpDelete("{commentId}")]
    public async Task<ActionResult<DeleteCommentResult>> DeleteComment(int commentId)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _commentService.DeleteCommentAsync(userId.Value, commentId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Gets all comments for a specific task.
    /// </summary>
    /// <param name="taskId"></param>
    /// <returns></returns>
    [HttpGet("task/{taskId}")]
    public async Task<ActionResult<TaskCommentsResponse>> GetCommentsByTaskId(int taskId)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var comments = await _commentService.GetCommentsByTaskIdAsync(userId.Value, taskId);
            return Ok(comments);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Gets paginated replies for a specific comment.
    /// </summary>
    /// <param name="commentId"></param>
    /// <param name="skip"></param>
    /// <param name="take"></param>
    /// <returns></returns>
    [HttpGet("comment/{commentId}")]
    public async Task<ActionResult<PaginatedRepliesResponse>> GetRepliesByCommentId(int commentId, [FromQuery] int skip = 0, [FromQuery] int take = 10)
    {
        if (skip < 0) skip = 0;
        if (take < 1) take = 1;
        if (take > 50) take = 50;

        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var replies = await _commentService.GetRepliesByCommentIdAsync(userId.Value, commentId, skip, take);
            return Ok(replies);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
