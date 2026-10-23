using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class LiveStreamChatServiceTest : BaseIntegrationTestFixture
{
    private ILiveStreamChatService ServiceUnderTest => ServiceProvider.GetRequiredService<ILiveStreamChatService>();

    public LiveStreamChatServiceTest(WebApplicationFactory<Program> webApplicationFactory) : base(webApplicationFactory)
    {
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

    private async Task AddStreamAsync(User host, TaskVisibility visibility, string roomName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem { Title = "task", UserId = host.Id, Visibility = visibility };
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();

        context.LiveStreams.Add(new LiveStream
        {
            Title = "live",
            RoomName = roomName,
            TaskId = task.Id,
            UserId = host.Id,
            Visibility = visibility
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SendAsync_PublicStream_PersistsMessages()
    {
        var viewer = await AddUserAsync("viewer@test.cool");
        var host = await AddUserAsync("host@test.cool");
        await AddStreamAsync(host, TaskVisibility.Public, "67 room");

        var response = await ServiceUnderTest.SendAsync(viewer.Id, "67 room", "BOSH");

        response.ShouldNotBeNull();
        response!.SenderId.ShouldBe(viewer.Id);
        response.Content.ShouldBe("BOSH");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var saved = context.LiveStreamMessages.Single();
        saved.Id.ShouldBe(response.Id);
        saved.Content.ShouldBe("BOSH");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public async Task SendAsync_WhenContentIsBlank_ThrowsAndDoesNotSave(string content)
    {
        var host = await AddUserAsync("host@test.cool");
        await AddStreamAsync(host, TaskVisibility.Public, "67 room");

        await Should.ThrowAsync<ArgumentException>(() => ServiceUnderTest.SendAsync(host.Id, "67 room", content));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.LiveStreamMessages.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenContentExceeds256Characters_ThrowsAndDoesNotSave()
    {
        var host = await AddUserAsync("host@test.cool");
        await AddStreamAsync(host, TaskVisibility.Public, "67 room");

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.SendAsync(host.Id, "67 room", new string('7', 257)));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.LiveStreamMessages.Any().ShouldBeFalse();
    }
    
    [Fact]
    public async Task SendAsync_OneWayFollowOnFriendsOnlyStream_ReturnsNullAndDoesNotSave()
    {
        var host = await AddUserAsync("host@test.cool");
        var viewer = await AddUserAsync("viewer@test.cool");
        await AddStreamAsync(host, TaskVisibility.FriendsOnly, "67 room");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Follow.Add(new Follow { FollowerId = viewer.Id, FollowedId = host.Id });
            await context.SaveChangesAsync();
        }

        var response = await ServiceUnderTest.SendAsync(viewer.Id, "67 room", "nope");

        response.ShouldBeNull();
        await using var saved = await DbContextFactory.CreateDbContextAsync();
        saved.LiveStreamMessages.Any().ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsync_ReturnsNewestPageInChronologicalOrder()
    {
        var host = await AddUserAsync("host@test.cool");
        await AddStreamAsync(host, TaskVisibility.Public, "67 room");
        for (var i = 1; i <= 5; i++)
        {
            await ServiceUnderTest.SendAsync(host.Id, "67 room", $"msg {i}");
            FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        }

        var page1 = await ServiceUnderTest.GetAsync(host.Id, "67 room", 1, 2);
        var page2 = await ServiceUnderTest.GetAsync(host.Id, "67 room", 2, 2);

        page1!.Messages.Select(m => m.Content).ShouldBe(["msg 4", "msg 5"]);
        page1.HasMore.ShouldBeTrue();
        page2!.Messages.Select(m => m.Content).ShouldBe(["msg 2", "msg 3"]);
    }

    [Fact]
    public async Task GetAsync_FriendsOnlyStreamForNonFriend_ReturnsNull()
    {
        var host = await AddUserAsync("host@test.cool");
        var stranger = await AddUserAsync("stranger@test.cool");
        
        await AddStreamAsync(host, TaskVisibility.FriendsOnly, "67 room");
        await ServiceUnderTest.SendAsync(host.Id, "67 room", "BAWSH");
        
        var page = await ServiceUnderTest.GetAsync(stranger.Id, "67 room", 1, 20);
        
        page.ShouldBeNull();
    }
}