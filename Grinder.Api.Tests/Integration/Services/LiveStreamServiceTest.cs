using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Text.Json;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class LiveStreamServiceTest : BaseIntegrationTestFixture
{
    private const string FakeSecret = "test-secret-test-secret-test-secret-6767";

    public LiveStreamServiceTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory) { }

    private sealed class FakeLiveKitRoomService : ILiveKitRoomService
    {
        private readonly IReadOnlyDictionary<string, int> _counts;
        public List<string> DeletedRooms { get; } = [];
        public FakeLiveKitRoomService(IReadOnlyDictionary<string, int> counts) => _counts = counts;

        public Task<IReadOnlyDictionary<string, int>> GetActiveRoomViewerCountsAsync()
            => Task.FromResult(_counts);

        public Task DeleteRoomAsync(string roomName)
        {
            DeletedRooms.Add(roomName);
            return Task.CompletedTask;
        }
    }

    private LiveStreamService BuildLiveStreamService(IReadOnlyDictionary<string, int>? activeRoomViewerCounts = null, bool withLiveKitConfig = true)
    {
        var settings = withLiveKitConfig
            ? new Dictionary<string, string?>
            {
                ["LIVEKIT_URL"] = "wss://test.livekit.invalid",
                ["LIVEKIT_API_KEY"] = "test-key",
                ["LIVEKIT_API_SECRET"] = FakeSecret,
            }
            : new Dictionary<string, string?>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var followService = ServiceProvider.GetRequiredService<IFollowService>();
        var roomService = new FakeLiveKitRoomService(activeRoomViewerCounts ?? new Dictionary<string, int>());
        return new LiveStreamService(DbContextFactory, configuration, followService, roomService, FakeTimeProvider);
    }

    private async Task<User> AddUserAsync(string email)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = new User
        {
            Email = email,
            DisplayName = email,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = FakeTimeProvider.GetUtcNow().UtcDateTime
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<TaskItem> AddTaskAsync(User owner, TaskVisibility visibility)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Title = "task",
            Description = "description",
            Visibility = visibility,
            UserId = owner.Id,
            CreatedAt = FakeTimeProvider.GetUtcNow()
        };
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private async Task FollowAsync(User follower, User followed)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.Add(new Follow { FollowerId = follower.Id, FollowedId = followed.Id });
        await context.SaveChangesAsync();
    }

    private async Task MakeFriendsAsync(User a, User b)
    {
        await FollowAsync(a, b);
        await FollowAsync(b, a);
    }

    private async Task<LiveStream> AddLiveAsync(User host, TaskVisibility visibility)
    {
        var task = await AddTaskAsync(host, visibility);
        return await BuildLiveStreamService().CreateLiveStreamAsync("live", task.Id, visibility, host.Id);
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_NoActiveRooms_ReturnsEmpty()
    {
        var viewer = await AddUserAsync("viewer@test.cool");

        var result = await BuildLiveStreamService().GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.ShouldBeEmpty();
        result.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_DbLiveNotInActiveRooms_IsExcluded()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var host = await AddUserAsync("host@test.cool");
        await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService().GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_OwnStream_ExcludedFromResults()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var live = await AddLiveAsync(viewer, TaskVisibility.Public);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 5 })
            .GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_FriendsOnlyStream_HiddenFromNonFriend()
    {
        var host = await AddUserAsync("host@test.cool");
        var viewer = await AddUserAsync("viewer@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 5 })
            .GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_FriendsOnlyStream_VisibleToFriend()
    {
        var host = await AddUserAsync("host@test.cool");
        var viewer = await AddUserAsync("viewer@test.cool");
        await MakeFriendsAsync(host, viewer);
        var live = await AddLiveAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 5 })
            .GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.ShouldHaveSingleItem().RoomName.ShouldBe(live.RoomName);
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_PublicStream_VisibleToAnyone()
    {
        var host = await AddUserAsync("host@test.cool");
        var stranger = await AddUserAsync("stranger@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 5 })
            .GetActiveLiveStreamsAsync(stranger.Id, limit: 20, excludeRoomNames: []);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_Ranking_FriendsBeforeFollowedBeforeOthers_RegardlessOfViewerCount()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var friend = await AddUserAsync("friend@test.cool");
        var followed = await AddUserAsync("followed@test.cool");
        var stranger = await AddUserAsync("stranger@test.cool");

        await MakeFriendsAsync(viewer, friend);
        await FollowAsync(viewer, followed);

        var friendLive = await AddLiveAsync(friend, TaskVisibility.Public);
        var followedLive = await AddLiveAsync(followed, TaskVisibility.Public);
        var strangerLive = await AddLiveAsync(stranger, TaskVisibility.Public);

        var counts = new Dictionary<string, int>
        {
            [friendLive.RoomName] = 1,
            [followedLive.RoomName] = 100,
            [strangerLive.RoomName] = 50
        };

        var result = await BuildLiveStreamService(counts).GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.Select(r => r.RoomName).ShouldBe(new[]
        {
        friendLive.RoomName, followedLive.RoomName, strangerLive.RoomName
    });
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_WithinGroup_SortedByViewerCountDescending()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var strangerA = await AddUserAsync("strangerA@test.cool");
        var strangerB = await AddUserAsync("strangerB@test.cool");

        var liveA = await AddLiveAsync(strangerA, TaskVisibility.Public);
        var liveB = await AddLiveAsync(strangerB, TaskVisibility.Public);

        var counts = new Dictionary<string, int> { [liveA.RoomName] = 3, [liveB.RoomName] = 10 };

        var result = await BuildLiveStreamService(counts).GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.Select(r => r.RoomName).ShouldBe(new[] { liveB.RoomName, liveA.RoomName });
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_TieOnViewerCount_MostRecentlyStartedFirst()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var strangerA = await AddUserAsync("strangerA@test.cool");
        var strangerB = await AddUserAsync("strangerB@test.cool");

        var liveA = await AddLiveAsync(strangerA, TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(10));
        var liveB = await AddLiveAsync(strangerB, TaskVisibility.Public);

        var counts = new Dictionary<string, int> { [liveA.RoomName] = 5, [liveB.RoomName] = 5 };

        var result = await BuildLiveStreamService(counts).GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: []);

        result.Items.Select(r => r.RoomName).ShouldBe(new[] { liveB.RoomName, liveA.RoomName });
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_LimitAboveMax_IsClampedTo50()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var host = await AddUserAsync("host@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 1 })
            .GetActiveLiveStreamsAsync(viewer.Id, limit: 1000, excludeRoomNames: []);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_ExcludedRoomNames_AreNotReturnedEvenIfStillActive()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var host = await AddUserAsync("host@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 5 })
            .GetActiveLiveStreamsAsync(viewer.Id, limit: 20, excludeRoomNames: [live.RoomName]);

        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_MoreItemsThanLimit_HasMoreIsTrue()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var hostA = await AddUserAsync("hostA@test.cool");
        var hostB = await AddUserAsync("hostB@test.cool");

        var liveA = await AddLiveAsync(hostA, TaskVisibility.Public);
        var liveB = await AddLiveAsync(hostB, TaskVisibility.Public);

        var counts = new Dictionary<string, int> { [liveA.RoomName] = 1, [liveB.RoomName] = 2 };

        var result = await BuildLiveStreamService(counts).GetActiveLiveStreamsAsync(viewer.Id, limit: 1, excludeRoomNames: []);

        result.Items.ShouldHaveSingleItem();
        result.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task GetActiveLiveStreamsAsync_ExactlyAtLimit_HasMoreIsFalse()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var host = await AddUserAsync("host@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService(new Dictionary<string, int> { [live.RoomName] = 1 })
            .GetActiveLiveStreamsAsync(viewer.Id, limit: 1, excludeRoomNames: []);

        result.Items.ShouldHaveSingleItem();
        result.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateToken_FriendsOnlyTaskStreamedPublically_IsRejected()
    {
        var host = await AddUserAsync("host@test.cool");
        var task = await AddTaskAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService().CreateTokenAsync(
            host.Id.ToString()
            , new TokenRequest("title",
                task.Id,
                (int)TaskVisibility.Public)
            );

        result.ShouldBeOfType<CreateTokenResult.ValidationFailed>();
        (await DbContextFactory.CreateDbContextAsync()).LiveStreams.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TaskVisibility.FriendsOnly, TaskVisibility.FriendsOnly)]
    [InlineData(TaskVisibility.Public, TaskVisibility.Public)]
    [InlineData(TaskVisibility.Public, TaskVisibility.FriendsOnly)]
    public async Task CreateToken_AllowedVisibilityCombo_SavesLiveWithChosenVis(TaskVisibility taskVisibility,
        TaskVisibility liveVisibility)
    {
        var host = await AddUserAsync("host@test.cool");
        var task = await AddTaskAsync(host, taskVisibility);

        var result = await BuildLiveStreamService().CreateTokenAsync(
            host.Id.ToString(),
            new TokenRequest("title", task.Id, (int)liveVisibility));

        result.ShouldBeOfType<CreateTokenResult.Success>();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.LiveStreams.SingleAsync()).Visibility.ShouldBe(liveVisibility);
    }

    [Theory]
    [InlineData(0)] // 0 is private
    [InlineData(3)]
    [InlineData(-1)]
    public async Task CreateToken_InvalidVisibility_IsRejected(int visibility)
    {
        var host = await AddUserAsync("host@test.cool");
        var task = await AddTaskAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService().CreateTokenAsync(
            host.Id.ToString(),
            new TokenRequest("title", task.Id, visibility));

        result.ShouldBeOfType<CreateTokenResult.ValidationFailed>();
    }

    [Fact]
    public async Task CreateToken_TaskNotYours_IsRejected()
    {
        var owner = await AddUserAsync("owner67@test.cool");
        var opp = await AddUserAsync("opp67@test.cool");
        var task = await AddTaskAsync(owner, TaskVisibility.Public);

        var result = await BuildLiveStreamService().CreateTokenAsync(
            opp.Id.ToString(), new TokenRequest("title", task.Id, (int)TaskVisibility.Public));

        result.ShouldBeOfType<CreateTokenResult.ValidationFailed>();
    }


    [Fact]
    public async Task ViewerToken_RoomDNE_ReturnsNotFound()
    {
        var viewer = await AddUserAsync("viewer67@test.cool");

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(viewer.Id, "livestream-does-not-exist");

        result.ShouldBeOfType<JoinTokenResult.NotFound>();
    }

    [Fact]
    public async Task ViewerToken_PublicLive_AnyoneCanJoin()
    {
        var host = await AddUserAsync("host@test.cool");
        var random = await AddUserAsync("rando@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(random.Id, live.RoomName);

        result.ShouldBeOfType<JoinTokenResult.Success>();
    }

    [Fact]
    public async Task ViewerToken_FriendsOnlyLive_HostCanJoinOwnLive()
    {
        var host = await AddUserAsync("host@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(host.Id, live.RoomName);
        result.ShouldBeOfType<JoinTokenResult.Success>();
    }

    [Fact]
    public async Task ViewerToken_FriendsOnlyLive_MutualFriendCanJoin()
    {
        var host = await AddUserAsync("host@test.cool");
        var friend = await AddUserAsync("friend@test.cool");
        await FollowAsync(host, friend);
        await FollowAsync(friend, host);
        var live = await AddLiveAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(friend.Id, live.RoomName);

        result.ShouldBeOfType<JoinTokenResult.Success>();
    }

    [Fact]
    public async Task ViewerToken_FriendsOnlyLive_StrangerIsNotFound()
    {
        var host = await AddUserAsync("host@test.cool");
        var stranger = await AddUserAsync("stranger@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(stranger.Id, live.RoomName);

        result.ShouldBeOfType<JoinTokenResult.NotFound>();
    }

    [Theory]
    [InlineData(true)]  // viewer follows host, host doesn't follow back
    [InlineData(false)] // host follows viewer, viewer doesn't follow back
    public async Task ViewerToken_FriendsOnlyLive_OneWayFollowIsNotFound(bool viewerFollowsHost)
    {
        var host = await AddUserAsync("host@test.cool");
        var viewer = await AddUserAsync("viewer@test.cool");
        if (viewerFollowsHost)
        {
            await FollowAsync(viewer, host);
        }
        else
        {
            await FollowAsync(host, viewer);
        }
        var live = await AddLiveAsync(host, TaskVisibility.FriendsOnly);

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(viewer.Id, live.RoomName);

        result.ShouldBeOfType<JoinTokenResult.NotFound>();
    }

    [Fact]
    public async Task ViewerToken_LiveKitConfigMissing_ReturnsConfigMissing()
    {
        var host = await AddUserAsync("host@test.cool");
        var viewer = await AddUserAsync("viewer@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService(withLiveKitConfig: false)
            .CreateViewerTokenAsync(viewer.Id, live.RoomName);

        result.ShouldBeOfType<JoinTokenResult.ConfigMissing>();
    }

    [Fact]
    public async Task ViewerToken_Success_TokenIsSubscribeOnly()
    {
        var host = await AddUserAsync("host@test.cool");
        var viewer = await AddUserAsync("viewer@test.cool");
        var live = await AddLiveAsync(host, TaskVisibility.Public);

        var result = await BuildLiveStreamService().CreateViewerTokenAsync(viewer.Id, live.RoomName);

        var success = result.ShouldBeOfType<JoinTokenResult.Success>();
        var payloadSegment = success.Token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payloadSegment = payloadSegment.PadRight(payloadSegment.Length + (4 - payloadSegment.Length % 4) % 4, '=');
        var payload = JsonDocument.Parse(Convert.FromBase64String(payloadSegment)).RootElement;

        payload.GetProperty("sub").GetString().ShouldBe(viewer.Id.ToString());
        var grants = payload.GetProperty("video");
        grants.GetProperty("room").GetString().ShouldBe(live.RoomName);
        grants.GetProperty("canSubscribe").GetBoolean().ShouldBeTrue();
        grants.GetProperty("canPublish").GetBoolean().ShouldBeFalse();
    }
}
