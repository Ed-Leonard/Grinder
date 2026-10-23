using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Livekit.Server.Sdk.Dotnet;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;

namespace Grinder.Api.Services;

public interface ILiveStreamService
{
    Task<LiveStream> CreateLiveStreamAsync(string title, int taskId, TaskVisibility visibility, int userId);
    Task<CreateTokenResult> CreateTokenAsync(string identity, TokenRequest request);
    Task<JoinTokenResult> CreateViewerTokenAsync(int viewerId, string roomName);
    Task<LiveStreamPageResponse> GetActiveLiveStreamsAsync(int viewerId, int limit, IReadOnlyCollection<string> excludeRoomNames);
    Task<bool> EndLiveStreamAsync(int userId, string roomName);
}

public interface ILiveKitRoomService
{
    Task<IReadOnlyDictionary<string, int>> GetActiveRoomViewerCountsAsync();
    Task DeleteRoomAsync(string roomName);
}

/// <summary>
/// Service responsible for interacting with the LiveKit server to retrieve information about active rooms and their viewer counts.
/// </summary>
public class LiveKitRoomService : ILiveKitRoomService
{
    private readonly IConfiguration _configuration;

    public LiveKitRoomService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Retrieves the viewer counts for all active rooms from the LiveKit server.
    /// </summary>
    /// <returns> A dictionary mapping room names to their respective viewer counts.</returns>
    public async Task<IReadOnlyDictionary<string, int>> GetActiveRoomViewerCountsAsync()
    {
        var url = _configuration["LIVEKIT_URL"] ?? _configuration["LiveKit:Url"];
        var apiKey = _configuration["LIVEKIT_API_KEY"] ?? _configuration["LiveKit:ApiKey"];
        var apiSecret = _configuration["LIVEKIT_API_SECRET"] ?? _configuration["LiveKit:ApiSecret"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return new Dictionary<string, int>();
        }

        var roomService = new RoomServiceClient(url, apiKey, apiSecret);
        var activeRooms = await roomService.ListRooms(new ListRoomsRequest());

        return activeRooms.Rooms.ToDictionary(r => r.Name, r => (int)r.NumParticipants);
    }

    /// <summary>
    /// Deletes the room.
    /// </summary>
    /// <param name="roomName">the room name</param>
    public async Task DeleteRoomAsync(string roomName)
    {
        var url = _configuration["LIVEKIT_URL"] ?? _configuration["LiveKit:Url"];
        var apiKey = _configuration["LIVEKIT_API_KEY"] ?? _configuration["LiveKit:ApiKey"];
        var apiSecret = _configuration["LIVEKIT_API_SECRET"] ?? _configuration["LiveKit:ApiSecret"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return;
        }

        var roomService = new RoomServiceClient(url, apiKey, apiSecret);

        try
        {
            await roomService.DeleteRoom(new DeleteRoomRequest { Room = roomName });
        }
        catch
        {
            // room is already gone i.e. stream is ended either way
        }
    }
}

/// <summary>
/// Responsible for creating livestream sessions with a guaranteed-unique LiveKit room name.
/// </summary>
public class LiveStreamService : ILiveStreamService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly IFollowService _followService;
    private readonly ILiveKitRoomService _liveKitRoomService;
    private readonly TimeProvider _timeProvider;

    public LiveStreamService(IDbContextFactory<DatabaseContext> dbContextFactory, IConfiguration configuration, IFollowService followService, ILiveKitRoomService liveKitRoomService, TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _followService = followService;
        _liveKitRoomService = liveKitRoomService;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Validates livestream request and attempts to create livekit cloud token for a stream
    /// </summary>
    /// <param name="identity"> The identity string of the user from their session cookie</param>
    /// <param name="request"> Contains stream title, task id, and stream visibiity</param>
    /// <returns> A CreateTokenResult which, if succesful will return a JWT token with the stream details</returns>
    public async Task<CreateTokenResult> CreateTokenAsync(string identity, TokenRequest request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new CreateTokenResult.ValidationFailed("Title is required.");
        }

        if (!int.TryParse(identity, out var userId))
        {
            return new CreateTokenResult.ValidationFailed("Invalid user identity.");
        }

        var task = await context.TaskItems
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TaskId && t.UserId == userId);

        if (task == null)
        {
            return new CreateTokenResult.ValidationFailed("Task not found or not owned by the user.");
        }
        var streamer = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (streamer == null)
        {
            return new CreateTokenResult.ValidationFailed("User not found.");
        }

        if (!TryMapVisibility(request.Visibility, out var visibility))
        {
            return new CreateTokenResult.ValidationFailed("Invalid visibility.");
        }

        if (task.Visibility == TaskVisibility.FriendsOnly && visibility == TaskVisibility.Public)
        {
            return new CreateTokenResult.ValidationFailed("A friends-only task cannot be streamed publicly.");
        }

        var url = _configuration["LIVEKIT_URL"] ?? _configuration["LiveKit:Url"];
        var apiKey = _configuration["LIVEKIT_API_KEY"] ?? _configuration["LiveKit:ApiKey"];
        var apiSecret = _configuration["LIVEKIT_API_SECRET"] ?? _configuration["LiveKit:ApiSecret"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return new CreateTokenResult.ConfigMissing("LiveKit server configuration is missing.");
        }

        if (await context.LiveStreams.AnyAsync(s => s.UserId == userId && s.EndedAt == null))
        {
            return new CreateTokenResult.AlreadyLive();
        }

        var livestream = await CreateLiveStreamAsync(request.Title, request.TaskId, visibility, userId);

        var token = new AccessToken(apiKey, apiSecret)
            .WithIdentity(identity)
            .WithName(streamer.DisplayName)
            .WithGrants(new VideoGrants
            {
                RoomJoin = true,
                Room = livestream.RoomName,
                CanPublish = true,
                CanSubscribe = true
            })
            .WithTtl(TimeSpan.FromMinutes(30))
            .ToJwt();

        return new CreateTokenResult.Success(url, token, livestream.RoomName);
    }

    /// <summary>
    /// Creates a subscribe-only LiveKit token for the viewer, if they are allowed to watch the stream then the host,
    /// anyone if it is public or mutual friends (friends-only) may join
    /// </summary>
    /// <param name="viewerId">The id of the user trying to watch</param>
    /// <param name="roomName">The LiveKit room name of the livestream</param>
    /// <returns></returns>
    public async Task<JoinTokenResult> CreateViewerTokenAsync(int viewerId, string roomName)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var live = await context.LiveStreams
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.RoomName == roomName && l.EndedAt == null);

        if (live == null)
        {
            return new JoinTokenResult.NotFound();
        }

        var viewer = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == viewerId);

        if (viewer == null)
        {
            return new JoinTokenResult.NotFound();
        }

        var isHost = live.UserId == viewerId;
        // checking is host, is live public OR mutual follow (friends)
        var allowed = isHost
            || live.Visibility == TaskVisibility.Public
            || (live.Visibility == TaskVisibility.FriendsOnly
                && await context.Follow.AnyAsync(f => f.FollowerId == viewerId && f.FollowedId == live.UserId)
                && await context.Follow.AnyAsync(f => f.FollowerId == live.UserId && f.FollowedId == viewerId));

        if (!allowed)
        {
            // return NotFound here instead of Forbidden for security of room existence
            return new JoinTokenResult.NotFound();
        }

        var url = _configuration["LIVEKIT_URL"] ?? _configuration["LiveKit:Url"];
        var apiKey = _configuration["LIVEKIT_API_KEY"] ?? _configuration["LiveKit:ApiKey"];
        var apiSecret = _configuration["LIVEKIT_API_SECRET"] ?? _configuration["LiveKit:ApiSecret"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return new JoinTokenResult.ConfigMissing("LiveKit server configuration is missing.");
        }

        var token = new AccessToken(apiKey, apiSecret)
            .WithIdentity(viewerId.ToString())
            .WithName(viewer.DisplayName)
            .WithGrants(new VideoGrants
            {
                RoomJoin = true,
                Room = roomName,
                CanPublish = false,
                CanSubscribe = true
            })
            .WithTtl(TimeSpan.FromMinutes(30))
            .ToJwt();

        return new JoinTokenResult.Success(url, token);
    }



    /// <summary>
    /// Validates that a provided TaskVisibility enum number is either Friend's Only or Public
    /// </summary>
    /// <param name="requestVisibility"> The inputted number that should correlate to a TaskVisibility value</param>
    /// <param name="visibility"> The out value of either a valid TaskVisibility or default</param>
    /// <returns> True if valid TaskVisibility otherwise false</returns>
    private static bool TryMapVisibility(int requestVisibility, out TaskVisibility visibility)
    {
        switch (requestVisibility)
        {
            case 2:
                visibility = TaskVisibility.Public;
                return true;
            case 1:
                visibility = TaskVisibility.FriendsOnly;
                return true;
            default:
                visibility = default;
                return false;
        }
    }

    /// <summary>
    /// Generates a unique livestream room name by attempting to create a random GUID-based name and checking for collisions in the database.
    /// If a unique name cannot be generated after 5 attempts, an InvalidOperationException is thrown.
    /// </summary>
    /// <param name="context"> The database context to use for checking for collisions.</param>
    /// <returns> A unique livestream room name.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static async Task<string> GenerateUniqueRoomNameAsync(DatabaseContext context)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"livestream-{Guid.NewGuid():N}";
            var exists = await context.LiveStreams.AnyAsync(l => l.RoomName == candidate);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique livestream room name.");
    }


    /// <summary>
    /// Creates a livestream entity in the db
    /// </summary>
    public async Task<LiveStream> CreateLiveStreamAsync(string title, int taskId, TaskVisibility visibility, int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var livestream = new LiveStream
        {
            Title = title,
            TaskId = taskId,
            UserId = userId,
            Visibility = visibility,
            RoomName = await GenerateUniqueRoomNameAsync(context),
            CreatedAt = _timeProvider.GetUtcNow()
        };

        context.LiveStreams.Add(livestream);
        await context.SaveChangesAsync();

        return livestream;
    }

    /// <summary>
    /// Ends the live stream
    /// </summary>
    /// <param name="userId">the user id</param>
    /// <param name="roomName">the room name</param>
    /// <returns></returns>
    public async Task<bool> EndLiveStreamAsync(int userId, string roomName)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var ownsRoom = await context.LiveStreams
            .AnyAsync(l => l.RoomName == roomName && l.UserId == userId);

        if (!ownsRoom)
        {
            return false;
        }
        
        var stream = await context.LiveStreams.
            FirstOrDefaultAsync(l => l.RoomName == roomName && l.UserId == userId && l.EndedAt == null);

        if (stream == null)
        {
            return false;
        }
        
        await _liveKitRoomService.DeleteRoomAsync(roomName);
        stream.EndedAt = _timeProvider.GetUtcNow();
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets a list of active livestreams that the specified viewer is permitted to see.
    /// </summary>
    /// <param name="viewerId"> The ID of the user requesting the list of active livestreams.</param>
    /// <param name="limit"> The number of results to return per page.</param>
    /// <param name="excludeRoomNames"> A list of room names to exclude from the results.</param>
    /// <returns> A list of LiveStreamPageResponse objects representing the active livestreams.</returns>
    public async Task<LiveStreamPageResponse> GetActiveLiveStreamsAsync(int viewerId, int limit, IReadOnlyCollection<string> excludeRoomNames)
    {
        limit = Math.Clamp(limit, 1, 50);

        var viewerCounts = await _liveKitRoomService.GetActiveRoomViewerCountsAsync();
        if (viewerCounts.Count == 0)
        {
            return new LiveStreamPageResponse { HasMore = false };
        }

        var candidates = await GetCandidateStreamsAsync(viewerCounts.Keys, viewerId);
        if (candidates.Count == 0)
        {
            return new LiveStreamPageResponse { HasMore = false };
        }

        var ranked = await RankAndFilterAsync(candidates, viewerCounts, viewerId);

        var remaining = ranked
            .Where(r => !excludeRoomNames.Contains(r.RoomName))
            .ToList();

        var pageItems = remaining.Take(limit).ToList();

        return new LiveStreamPageResponse
        {
            Items = pageItems,
            HasMore = remaining.Count > limit
        };
    }

    /// <summary>
    /// Fetches our DB records for the given active LiveKit room names, with streamer info included.
    /// Excludes any streams where the streamer is the same as the viewer (to avoid showing a user their own stream).
    /// </summary>
    /// <param name="activeRoomNames"> The names of the active LiveKit rooms.</param>
    /// <param name="viewerId"> The ID of the user requesting the list of active livestreams.</param>
    /// <returns> A list of LiveStream entities corresponding to the active rooms.</returns>
    private async Task<List<LiveStream>> GetCandidateStreamsAsync(IEnumerable<string> activeRoomNames, int viewerId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        return await context.LiveStreams
            .Include(l => l.User)
            .Include(l => l.Task)
            .Where(l => l.EndedAt == null && activeRoomNames.Contains(l.RoomName) && l.UserId != viewerId)
            .ToListAsync();
    }

    /// <summary>
    /// Filters out Friends Only streams the viewer isn't permitted to see, ranks the rest by
    /// relationship to the streamer (friends, then followed, then everyone else), and sorts
    /// within each group by viewer count descending then most recently started.
    /// </summary>
    /// <param name="candidates"> The list of candidate LiveStream entities to filter and rank.</param>
    /// <param name="viewerCounts"> A dictionary mapping room names to their live participant counts.</param>
    /// <param name="viewerId"> The ID of the user requesting the list of active livestreams.</param>
    /// <returns> A list of LiveStreamSummaryResponse objects representing the filtered and ranked livestreams.</returns>
    private async Task<List<LiveStreamSummaryResponse>> RankAndFilterAsync(
        List<LiveStream> candidates,
        IReadOnlyDictionary<string, int> viewerCounts,
        int viewerId)
    {
        var streamerIds = candidates.Select(c => c.UserId).Distinct().ToList();
        var relationships = await _followService.GetRelationshipStatusesAsync(viewerId, streamerIds);

        var ranked = new List<(LiveStreamSummaryResponse Response, int Rank)>();

        foreach (var stream in candidates)
        {
            var relationship = relationships.GetValueOrDefault(stream.UserId, RelationshipStatus.Follow);

            if (!LiveStreamVisibilityHelper.IsVisibleToViewer(stream.Visibility, relationship))
            {
                continue;
            }

            var rank = LiveStreamVisibilityHelper.GetRank(relationship);

            var response = new LiveStreamSummaryResponse
            {
                Id = stream.Id,
                RoomName = stream.RoomName,
                Title = stream.Title,
                TaskTitle = stream.Task!.Title,
                TaskId = stream.TaskId,
                Streamer = new LiveStreamerResponse
                {
                    Id = stream.User!.Id,
                    DisplayName = stream.User.DisplayName,
                },
                ViewerCount = viewerCounts[stream.RoomName],
                StartedAt = stream.CreatedAt,
                Visibility = stream.Visibility
            };

            ranked.Add((response, rank));
        }

        return ranked
            .OrderBy(r => r.Rank)
            .ThenByDescending(r => r.Response.ViewerCount)
            .ThenByDescending(r => r.Response.StartedAt)
            .Select(r => r.Response)
            .ToList();
    }
}
