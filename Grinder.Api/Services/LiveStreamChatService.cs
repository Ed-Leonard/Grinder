using Grinder.Api.DataAccess;
using Grinder.Api.Models;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Services;


/// <summary>
/// Persists livestream chat and loads it back a page at a time.
/// Used by ChatHub when a message is sent, and by the livestream controller for history.
/// </summary>

public interface ILiveStreamChatService
{
    Task<LiveChatMessageResponse?> SendAsync(int senderId, string roomName, string content);
    Task<LiveChatPageResponse?> GetAsync(int userId, string roomName, int page, int limit);
}

public class LiveStreamChatService : ILiveStreamChatService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public LiveStreamChatService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Saves a livestream chat message. Returns null if the room does not exist or the sender cannot watch it.
    /// </summary>
    /// <param name="senderId">The id of the user sending the message.</param>
    /// <param name="roomName">The LiveKit room name of the livestream.</param>
    /// <param name="content">The message text. Must be between 1 and 256 characters.</param>
    /// <returns>
    /// The saved message, or null if the room does not exist or the sender is not allowed to watch it.
    /// </returns>
    public async Task<LiveChatMessageResponse?> SendAsync(int senderId, string roomName, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 256)
        {
            throw new ArgumentException("Message must be between 1 and 256 characters");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var stream = await FindVisibleStreamAsync(db, senderId, roomName);

        if (stream == null)
        {
            return null;
        }
        var sender = await db.Users.FirstOrDefaultAsync(u => u.Id == senderId);
        if (sender == null)
        {
            return null;
        }

        var message = new LiveStreamMessage
        {
            LiveStreamId = stream.Id,
            SenderId = senderId,
            Sender = sender,
            Content = content,
            SentAt = _timeProvider.GetUtcNow().UtcDateTime,
        };

        db.LiveStreamMessages.Add(message);
        await db.SaveChangesAsync();

        return new LiveChatMessageResponse(message.Id, stream.RoomName, message.SenderId, message.Sender.DisplayName, message.Content, message.SentAt);
    }


    /// <summary>
    /// Returns one page of chat for a room, oldest message first.
    /// Returns null if the room does not exist or the user cannot watch it.
    /// </summary>
    /// <param name="userId">The id of the user loading the chat.</param>
    /// <param name="roomName">The LiveKit room name of the livestream.</param>
    /// <param name="page">The page number, starting at 1.</param>
    /// <param name="limit">How many messages to return. Clamped to between 1 and 100.</param>
    /// <returns>
    /// The page of messages in chronological order, or null if the room does not exist or the user is not allowed to watch it.
    /// </returns>
    public async Task<LiveChatPageResponse?> GetAsync(int userId, string roomName, int page, int limit)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var stream = await FindVisibleStreamAsync(db, userId, roomName);

        if (stream == null)
        {
            return null;
        }

        page = Math.Max(page, 1);
        limit = Math.Clamp(limit, 1, 100);

        var messages = await db.LiveStreamMessages
            .Where(m => m.LiveStreamId == stream.Id)
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(m => new LiveChatMessageResponse(m.Id, stream.RoomName, m.SenderId, m.Sender.DisplayName, m.Content, m.SentAt))
            .ToListAsync();

        messages.Reverse();
        return new LiveChatPageResponse(messages, page, messages.Count == limit);
    }

    private static async Task<LiveStream?> FindVisibleStreamAsync(DatabaseContext db, int userId, string roomName)
    {
        var stream = await db.LiveStreams
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.RoomName == roomName);

        if (stream == null)
        {
            return null;
        }

        // is it my stream, public or am I friends with the owner and the stream is friends-only
        var allowed = stream.UserId == userId
                      || stream.Visibility == TaskVisibility.Public
                      || (stream.Visibility == TaskVisibility.FriendsOnly
                          && await db.Follow.AnyAsync(
                              f => f.FollowerId == userId
                                   && f.FollowedId == stream.UserId
                                   )
                          && await db.Follow.AnyAsync(f => f.FollowerId == stream.UserId
                          && f.FollowedId == userId)
                      );
        return allowed ? stream : null;
    }
}