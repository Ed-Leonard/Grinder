using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Services;
using System.Security.Claims;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;

namespace Grinder.Api.Controllers;

/// <summary>
/// Handles direct message history requests.
/// </summary>
/// <remarks>
/// Real-time sending/receiving is handled by ChatHub (SignalR); this
/// controller serves persisted conversation previews and history over REST.
/// </remarks>
[ApiController]
[Route("api/directmessages")]
[Authorize]
public class DirectMessageController : ControllerBase
{
	private readonly IDirectMessageService _messageService;

	public DirectMessageController(IDirectMessageService messageService)
	{
		_messageService = messageService;
	}

	/// <summary>
	/// Retrieves the message history between the authenticated user and another user.
	/// </summary>
	/// <param name="otherUserId">The other participant in the conversation.</param>
	/// <returns>
	/// HTTP OK with the ordered list of messages between the two users,
	/// or 403 if they are not mutual friends.
	/// </returns>
	[HttpGet("conversation/{otherUserId}")]
	public async Task<ActionResult> GetConversation(int otherUserId, [FromQuery] int page = 1, [FromQuery] int limit = 20)
	{
		var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
		var messages = await _messageService.GetConversationAsync(userId, otherUserId, page, limit);
		if (messages is null)
		{
			return StatusCode(StatusCodes.Status403Forbidden);
		}

		return Ok(new ConversationPageResponse(messages, page, messages.Count == limit));
	}

	/// <summary>
	/// Retrieves a preview of each conversation the authenticated user has with a friend,
	/// including the latest message and unread count.
	/// </summary>
	/// <returns>
	/// HTTP OK with the list of conversation previews, most recent first.
	/// </returns>
	[HttpGet("conversations")]
	public async Task<ActionResult> GetConversationsPreviews()
	{
		var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
		var previews = await _messageService.GetConversationPreviewsAsync(userId);
		return Ok(previews);
	}


	/// <summary>
	/// Retrieves a list of friends who are able to view a task, and that you have not already shared it with, based on a task id
	/// </summary>
	/// <param name="taskId">The id of the task</param>
	/// <returns>
	/// HTTP OK with a list of Friend DTOs
	/// HTTP BADREQUEST if the task does not exist or user does not have permission to view
	/// </returns>
	[HttpGet("{taskId:int}/shareable-friends")]
	public async Task<ActionResult<IReadOnlyList<FriendResponse>>> GetShareableFriends(int taskId)
	{
		if (Session.GetUserId(HttpContext) is not int userId)
		{
			return Unauthorized();
		}

		try
		{
			var friends = await _messageService.GetShareableFriendsForTaskAsync(userId, taskId);
			return Ok(friends);
		}
		catch (ArgumentException ex)
		{
			return BadRequest(new { error = ex.Message });
		}
	}

	/// <summary>
	/// Shares a task to a list of friends by creating a direct message
	/// </summary>
	/// <param name="request">The task id to share and the list of friends</param>
	/// <returns>
	/// HTTP OK with a list of messages
	/// HTTP BADREQUEST if the task does not exist or user does not have permission to view
	/// </returns>
	[HttpPost("share-task")]
	public async Task<ActionResult<List<DirectMessageResponse>>> ShareTask([FromBody] ShareTaskRequest request)
	{
		if (Session.GetUserId(HttpContext) is not int userId)
		{
			return Unauthorized();
		}

		try
		{
			var results = await _messageService.ShareTaskWithManyAsync(
				userId, request.ReceiverIds, request.TaskId);
			return Ok(results);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { error = ex.Message });
		}
	}

	public record ShareTaskRequest(int TaskId, List<int> ReceiverIds);
}
