using Microsoft.AspNetCore.SignalR;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace Grinder.Api.Hubs;

/// <summary>
/// SignalR hub for direct messages and livestream chat.
/// Direct messages use Clients.User to reach one recipient, then echo to the caller.
/// Livestream chat uses a group named by the LiveKit room name.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IDirectMessageService _messageService;
    private readonly ILiveStreamChatService _liveStreamChatService;
    public ChatHub(IDirectMessageService messageService,  ILiveStreamChatService liveStreamChatService)
    {
        _messageService = messageService;
        _liveStreamChatService = liveStreamChatService;
    }

    /// <summary>
    /// Sends a direct message from the connected (authenticated) user to a
    /// specific recipient. Persists the message, then pushes it to the
    /// recipient's active connections (Clients.User) and echoes it back to
    /// the sender's own connection (Clients.Caller) so their UI updates too.
    /// Clients.User only reaches the other user, not the sender.
    /// </summary>
    /// <param name="receiverId">User ID of the message recipient</param>
    /// <param name="content">The message text</param>
    public async Task SendMessage(int receiverId, string content)
    {
        var senderId = int.Parse(Context.UserIdentifier!);
        var message = await _messageService.SendAsync(senderId, receiverId, content);

        await Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", message);
        await Clients.Caller.SendAsync("ReceiveMessage", message);
    }

    /// <summary>
    /// Marks the conversation as read
    /// </summary>
    /// <param name="receiverId">User ID of the message recipient</param>
    public async Task MarkConversationRead(int receiverId)
    {
        var userId = int.Parse(Context.UserIdentifier!);
        await _messageService.MarkConversationReadAsync(userId, receiverId);
    }

    /// <summary>
    /// Adds the caller's connection to the livestream chat group for this room.
    /// The group name is the LiveKit room name.
    /// </summary>
    /// <param name="roomName">The LiveKit room name of the livestream.</param>
    /// <exception cref="HubException">
    /// Thrown when the room does not exist or the user is not allowed to watch it.
    /// </exception>
    public async Task JoinStream(string roomName)
    {
        var userId = int.Parse(Context.UserIdentifier!);
        if (await _liveStreamChatService.GetAsync(userId, roomName, 1, 1) is null)
        {
            throw new HubException("Not allowed");
        }
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
    }

    /// <summary>
    /// Removes the caller's connection from the livestream chat group.
    /// </summary>
    /// <param name="roomName">The LiveKit room name of the livestream.</param>
    public async Task LeaveStream(string roomName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
    }

    /// <summary>
    /// Saves a livestream chat message, then pushes it to everyone else in the room
    /// and echoes it to the caller. Clients receive it as "ReceiveLiveMessage".
    /// </summary>
    /// <param name="roomName">The LiveKit room name of the livestream.</param>
    /// <param name="content">The message text. Must be between 1 and 256 characters.</param>
    /// <exception cref="HubException">
    /// Thrown when the room does not exist or the sender is not allowed to watch it.
    /// </exception>
    public async Task SendLiveMessage(string roomName, string content)
    {
        var senderId = int.Parse(Context.UserIdentifier!);
        var message = await _liveStreamChatService.SendAsync(senderId, roomName, content);
        if (message is null)
        {
            throw new HubException("Not allowed");
        }
        
        await Clients.OthersInGroup(roomName).SendAsync("ReceiveLiveMessage", message);
        await Clients.Caller.SendAsync("ReceiveLiveMessage", message);
    }
}