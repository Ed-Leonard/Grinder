using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class ClipServiceTest : BaseIntegrationTestFixture
{
    private IClipService ServiceUnderTest => ServiceProvider.GetRequiredService<IClipService>();

    public ClipServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUserToDatabaseAsync(string email, string displayName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = FakeTimeProvider.GetUtcNow().UtcDateTime
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private async Task MakeFriendsAsync(User a, User b)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.AddRange(
            new Follow { FollowerId = a.Id, FollowedId = b.Id },
            new Follow { FollowerId = b.Id, FollowedId = a.Id });
        await context.SaveChangesAsync();
    }

    private async Task<TaskItem> AddClipAsync(
        User owner,
        string title,
        TaskVisibility visibility,
        bool isClip = true,
        int? parentTaskId = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Title = title,
            Description = "description",
            Visibility = visibility,
            IsClip = isClip,
            ParentTaskId = parentTaskId,
            UserId = owner.Id,
            CreatedAt = FakeTimeProvider.GetUtcNow()
        };

        context.TaskItems.Add(task);
        await context.SaveChangesAsync();

        return task;
    }

    [Fact]
    public async Task GetClipsAsync_NoClips_ReturnsEmptyFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.ShouldBeEmpty();
        clips.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task GetClipsAsync_PublicClipFromStranger_ReturnsClipInFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var clip = await AddClipAsync(stranger, "Public clip", TaskVisibility.Public);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        var item = clips.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(clip.Id);
        item.UserId.ShouldBe(stranger.Id);
        item.UserDisplayName.ShouldBe(stranger.DisplayName);
        item.Title.ShouldBe("Public clip");
    }

    [Fact]
    public async Task GetClipsAsync_FriendsOnlyClipFromFriend_ReturnsClipInFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(viewer, friend);

        await AddClipAsync(friend, "Friends only clip", TaskVisibility.FriendsOnly);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetClipsAsync_FriendsOnlyClipFromStranger_ExcludesFromFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        await AddClipAsync(stranger, "Friends only clip", TaskVisibility.FriendsOnly);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetClipsAsync_PrivateClip_ExcludesFromFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(viewer, friend);

        await AddClipAsync(friend, "Private clip", TaskVisibility.Private);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetClipsAsync_OwnPublicClip_ExcludedFromFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        await AddClipAsync(viewer, "My own public clip", TaskVisibility.Public);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetClipsAsync_NonClipTask_ExcludedFromFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        await AddClipAsync(stranger, "Regular public task", TaskVisibility.Public, isClip: false);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetClipsAsync_SubtaskExists_ExcludesSubtaskFromFeed()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var parent = await AddClipAsync(stranger, "Parent clip", TaskVisibility.Public);
        await AddClipAsync(stranger, "Subtask clip", TaskVisibility.Public, parentTaskId: parent.Id);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        var item = clips.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task GetClipsAsync_MapsClipFields()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var clip = await AddClipAsync(stranger, "Public clip", TaskVisibility.Public);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        var item = clips.Items.ShouldHaveSingleItem();
        item.Description.ShouldBe("description");
        item.CreatedAt.ShouldBe(clip.CreatedAt);
        item.TaskAttachments.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetClipsAsync_OrdersByMostRecentFirst()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var first = await AddClipAsync(stranger, "First clip", TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var second = await AddClipAsync(stranger, "Second clip", TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var third = await AddClipAsync(stranger, "Third clip", TaskVisibility.Public);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 20);

        clips.Items.Select(item => item.Id).ShouldBe(new[] { third.Id, second.Id, first.Id });
    }

    [Fact]
    public async Task GetClipsAsync_Pagination_ReturnsRequestedPageAndTotalPages()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        for (var i = 0; i < 5; i++)
        {
            await AddClipAsync(stranger, $"Clip {i}", TaskVisibility.Public);
        }

        var firstPage = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 2);
        var secondPage = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 2, limit: 2);

        firstPage.Items.Count.ShouldBe(2);
        firstPage.Page.ShouldBe(1);
        firstPage.TotalPages.ShouldBe(3);

        secondPage.Items.Count.ShouldBe(2);
        secondPage.Page.ShouldBe(2);
        firstPage.Items.Select(item => item.Id)
            .ShouldNotContain(id => secondPage.Items.Select(second => second.Id).Contains(id));
    }

    [Fact]
    public async Task GetClipsAsync_LimitAboveMax_IsClampedTo50()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        for (var i = 0; i < 3; i++)
        {
            await AddClipAsync(stranger, $"Clip {i}", TaskVisibility.Public);
        }

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 1, limit: 1000);

        clips.Items.Count.ShouldBe(3);
        clips.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task GetClipsAsync_PageBelowOne_IsClampedToFirstPage()
    {
        var viewer = await AddUserToDatabaseAsync("viewer@example.com", "viewer");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        await AddClipAsync(stranger, "Only clip", TaskVisibility.Public);

        var clips = await ServiceUnderTest.GetClipsAsync(viewer.Id, page: 0, limit: 20);

        clips.Page.ShouldBe(1);
        clips.Items.ShouldHaveSingleItem();
    }
}
