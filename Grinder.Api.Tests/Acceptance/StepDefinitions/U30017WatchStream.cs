using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U30017WatchLiveStreamSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private readonly Dictionary<string, (User User, LiveStream Stream)> _streamers = new();
    private LiveStreamPageResponse? _page;
    private LiveChatPageResponse? _chat;
    private string? _liveMessage;

    public U30017WatchLiveStreamSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<User> CreateStreamAsync(string name, TaskVisibility visibility, int viewerCount)
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();

        var user = new User
        {
            Email = $"{name.ToLowerInvariant()}@test.com",
            DisplayName = name,
            PasswordHash = "unused",
            Country = "NZ",
            IsVerified = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var task = new TaskItem
        {
            UserId = user.Id,
            Title = $"{name}'s task",
            Visibility = visibility
        };
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();

        var stream = new LiveStream
        {
            Title = $"{name}'s stream",
            RoomName = $"livestream-{Guid.NewGuid():N}",
            TaskId = task.Id,
            UserId = user.Id,
            Visibility = visibility,
            CreatedAt = _fixture.AcceptanceTestNow
        };
        context.LiveStreams.Add(stream);
        await context.SaveChangesAsync();

        _fixture.LiveKitRoomService.ActiveRooms[stream.RoomName] = viewerCount;
        _streamers[name] = (user, stream);
        return user;
    }

    private async Task MakeFriendsAsync(User other)
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.Follow.AddRange(
            new Follow { FollowerId = _fixture.CurrentUserId!.Value, FollowedId = other.Id },
            new Follow { FollowerId = other.Id, FollowedId = _fixture.CurrentUserId.Value });
        await context.SaveChangesAsync();
    }

    private async Task FollowAsync(User other)
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.Follow.Add(new Follow { FollowerId = _fixture.CurrentUserId!.Value, FollowedId = other.Id });
        await context.SaveChangesAsync();
    }

    [Given(@"another user is streaming publicly")]
    public async Task GivenAnotherUserIsStreamingPublicly()
    {
        await CreateStreamAsync("Streamer", TaskVisibility.Public, viewerCount: 1);
    }

    [Given(@"another user is streaming to friends only")]
    public async Task GivenAnotherUserIsStreamingToFriendsOnly()
    {
        await CreateStreamAsync("Streamer", TaskVisibility.FriendsOnly, viewerCount: 1);
    }

    [Given(@"I am friends with that user")]
    public async Task GivenIAmFriendsWithThatUser()
    {
        await MakeFriendsAsync(_streamers["Streamer"].User);
    }

    [Given(@"a friend named ""(.*)"" is streaming publicly with (\d+) viewers?")]
    public async Task GivenAFriendNamedIsStreamingPubliclyWith(string name, int viewers)
    {
        var user = await CreateStreamAsync(name, TaskVisibility.Public, viewers);
        await MakeFriendsAsync(user);
    }

    [Given(@"a user I follow named ""(.*)"" is streaming publicly with (\d+) viewers?")]
    public async Task GivenAUserIFollowNamedIsStreamingPubliclyWith(string name, int viewers)
    {
        var user = await CreateStreamAsync(name, TaskVisibility.Public, viewers);
        await FollowAsync(user);
    }

    [Given(@"a stranger named ""(.*)"" is streaming publicly with (\d+) viewers?")]
    public async Task GivenAStrangerNamedIsStreamingPubliclyWith(string name, int viewers)
    {
        await CreateStreamAsync(name, TaskVisibility.Public, viewers);
    }

    [Given(@"some time passes")]
    public void GivenSomeTimePasses()
    {
        _fixture.SetNow(_fixture.AcceptanceTestNow.AddMinutes(1));
    }

    [When(@"I view the live streams list")]
    public async Task WhenIViewTheLiveStreamsList()
    {
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync("api/livestreams/active?limit=20");
        _fixture.LastResponse.EnsureSuccessStatusCode();
        _page = await _fixture.LastResponse.Content.ReadFromJsonAsync<LiveStreamPageResponse>();
    }

    [Then(@"I see that stream in the list")]
    public void ThenISeeThatStreamInTheList()
    {
        _page.ShouldNotBeNull();
        var stream = _streamers["Streamer"].Stream;
        _page!.Items.ShouldContain(i => i.RoomName == stream.RoomName);
    }

    [Then(@"I do not see that stream in the list")]
    public void ThenIDoNotSeeThatStreamInTheList()
    {
        _page.ShouldNotBeNull();
        var stream = _streamers["Streamer"].Stream;
        _page!.Items.ShouldNotContain(i => i.RoomName == stream.RoomName);
    }

    [Then(@"the streams appear in this order:")]
    public void ThenTheStreamsAppearInThisOrder(Table table)
    {
        _page.ShouldNotBeNull();
        var expectedRoomNames = table.Rows
            .Select(row => _streamers[row["name"]].Stream.RoomName)
            .ToList();

        _page!.Items.Select(i => i.RoomName).ShouldBe(expectedRoomNames);
    }

    [Given("I am viewing a stream")]
    public async Task GivenIAmViewingAStream()
    {
        await CreateStreamAsync("Streamer", TaskVisibility.Public, viewerCount: 1);
        
        _liveMessage = "bosh";
        var (user, stream) = _streamers["Streamer"];
        var chat = _fixture.ServiceProvider.GetRequiredService<ILiveStreamChatService>();
        var saved = await chat.SendAsync(user.Id, stream.RoomName, _liveMessage);
        saved.ShouldNotBeNull();
    }

    [Given("I am watching a stream")]
    public async Task GivenIAmWatchingAStream()
    {
        await CreateStreamAsync("Streamer", TaskVisibility.Public, viewerCount: 1);
    }

    [Given("I have that stream's chat open")]
    [When("I click on the chat icon")]
    public async Task WhenTheChatIsOpened()
    {
        var roomName = _streamers["Streamer"].Stream.RoomName;
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync(
            $"api/livestreams/{Uri.EscapeDataString(roomName)}/messages");
        if (_fixture.LastResponse.IsSuccessStatusCode)
        {
            _chat = await _fixture.LastResponse.Content.ReadFromJsonAsync<LiveChatPageResponse>();
        }
    }

    [Then("the chat opens")]
    public void ThenTheChatOpens()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
        _chat.ShouldNotBeNull();
    }

    [Then("I can see chat messages that are being sent at that time.")]
    public void ThenICanSeeChatMessagesThatAreBeingSentAtThatTime()
    {
        _chat.ShouldNotBeNull();
        _chat!.Messages.ShouldContain(m => m.Content == _liveMessage);
    }

    [When("I send a message in the chat between 1 and 256 characters")]
    public async Task WhenISendAMessageInTheChatBetween1And256Characters()
    {
        _liveMessage = "what's up guys!";
        _liveMessage.Length.ShouldBeInRange(1, 256);

        var roomName = _streamers["Streamer"].Stream.RoomName;
        var chat = _fixture.ServiceProvider.GetRequiredService<ILiveStreamChatService>();
        var saved = await chat.SendAsync(_fixture.CurrentUserId!.Value, roomName, _liveMessage);
        saved.ShouldNotBeNull();
    }

    [Then("the message is displayed to all viewers")]
    public async Task ThenTheMessageIsDisplayedToAllViewers()
    {
        var roomName = _streamers["Streamer"].Stream.RoomName;
        var response = await _fixture.HttpClient.GetAsync(
            $"api/livestreams/{Uri.EscapeDataString(roomName)}/messages");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<LiveChatPageResponse>();
        page.ShouldNotBeNull();
        page!.Messages.ShouldContain(m => m.Content == _liveMessage && m.SenderId == _fixture.CurrentUserId);
    }

    [Then("the streamer can see it in real time.")]
    public async Task ThenTheStreamerCanSeeItInRealTime()
    {
        var (user, stream) = _streamers["Streamer"];
        var chat = _fixture.ServiceProvider.GetRequiredService<ILiveStreamChatService>();
        var page = await chat.GetAsync(user.Id, stream.RoomName, 1, 20);

        page.ShouldNotBeNull();
        page!.Messages.ShouldContain(m => m.Content == _liveMessage && m.SenderId == _fixture.CurrentUserId);
    }
}