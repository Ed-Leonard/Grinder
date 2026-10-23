using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using System.Security.Claims;

namespace Grinder.Api.Controllers;

/// <summary>
///  API controller responsible for managing notifications for authenticated users.
///  Provides endpoints for retrieving, marking as read, and dismissing notifications.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    ///  Retrieves a paginated list of notifications for the authenticated user, with optional filtering by type and unread status.
    /// </summary>
    /// <param name="page">The page number to retrieve.</param>
    /// <param name="limit">The number of notifications per page.</param>
    /// <param name="type">Optional filter by notification type.</param>
    /// <param name="unreadOnly">If true, only unread notifications are returned.</param>
    /// <returns>A paginated list of notifications.</returns>
    [HttpGet]
    public async Task<ActionResult<NotificationPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] NotificationType? type = null,
        [FromQuery] bool unreadOnly = false)
    {
        var result = await _notificationService.ListAsync(CurrentUserId, page, limit, type, unreadOnly);
        return Ok(result);
    }

    /// <summary>
    ///  Retrieves the count of unread notifications for the authenticated user.
    /// </summary>
    /// <returns>The count of unread notifications.</returns>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> UnreadCount()
    {
        var count = await _notificationService.UnreadCountAsync(CurrentUserId);
        return Ok(new { count });
    }

    /// <summary>
    /// Marks a specific notification as read for the authenticated user.
    /// </summary>
    /// <param name="id">The ID of the notification to mark as read.</param>
    /// <returns>No content if successful; NotFound if the notification does not exist; 403 Forbidden if the user is not authorized.</returns>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        try
        {
            await _notificationService.MarkReadAsync(CurrentUserId, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>
    ///  Marks all notifications as read for the authenticated user.
    /// </summary>
    /// <returns>No content if successful.</returns>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkReadAsync(CurrentUserId);
        return NoContent();
    }

    /// <summary>
    ///  Dismisses a specific notification for the authenticated user, effectively deleting it from their notification list.
    /// </summary>
    /// <param name="id">The ID of the notification to dismiss.</param>
    /// <returns>No content if successful; NotFound if the notification does not exist; 403 Forbidden if the user is not authorized.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Dismiss(int id)
    {
        try
        {
            await _notificationService.DeleteAsync(CurrentUserId, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {

            return StatusCode(StatusCodes.Status403Forbidden);
        }
    }
}
