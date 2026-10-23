using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class FeedServiceTest : BaseIntegrationTestFixture
{
    private IFeedService ServiceUnderTest => ServiceProvider.GetRequiredService<IFeedService>();

    public FeedServiceTest(WebApplicationFactory<Program> webAppFactory)
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

    private async Task<TaskItem> AddTaskAsync(
        User owner,
        string title,
        TaskVisibility visibility,
        TaskItemStatus status = TaskItemStatus.Todo,
        int? parentTaskId = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Title = title,
            Description = "description",
            Visibility = visibility,
            ItemStatus = status,
            ParentTaskId = parentTaskId,
            UserId = owner.Id,
            CreatedAt = FakeTimeProvider.GetUtcNow()
        };

        context.TaskItems.Add(task);
        await context.SaveChangesAsync();

        return task;
    }

    private async Task<Reaction> AddReactionToDatabaseAsync(string name = "thumbsup")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var reaction = new Reaction { Name = name, FileName = $"{name}.png" };
        context.Reactions.Add(reaction);
        await context.SaveChangesAsync();
        return reaction;
    }

    private async Task AddTaskReactionAsync(TaskItem task, Reaction reaction, User user)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskReactions.Add(new TaskReaction
        {
            TaskItemId = task.Id,
            ReactionId = reaction.Id,
            UserId = user.Id,
            CreatedAt = FakeTimeProvider.GetUtcNow().UtcDateTime
        });
        await context.SaveChangesAsync();
    }
    private async Task<TaskView?> GetTaskViewAsync(int userId, int taskItemId)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        return await context.TaskViews
            .SingleOrDefaultAsync(v => v.UserId == userId && v.TaskItemId == taskItemId);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_NoFriends_ReturnsEmptyFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.ShouldBeEmpty();
        feed.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_FriendHasPublicTask_ReturnsTaskInFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var task = await AddTaskAsync(friend, "Public task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(task.Id);
        item.UserId.ShouldBe(friend.Id);
        item.UserDisplayName.ShouldBe(friend.DisplayName);
        item.Title.ShouldBe("Public task");
    }

    [Fact]
    public async Task GetFriendsFeedAsync_FriendHasFriendsOnlyTask_ReturnsTaskInFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        await AddTaskAsync(friend, "Friends only task", TaskVisibility.FriendsOnly);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_FriendHasPrivateTask_ExcludesFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        await AddTaskAsync(friend, "Private task", TaskVisibility.Private);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_OneWayFollow_IncludesTasksFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var oneWay = await AddUserToDatabaseAsync("one-way@example.com", "one way");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Follow.Add(new Follow { FollowerId = owner.Id, FollowedId = oneWay.Id });
            await context.SaveChangesAsync();
        }

        await AddTaskAsync(oneWay, "Not a friend's task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_SubtaskExists_ExcludesSubtaskFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var parent = await AddTaskAsync(friend, "Parent task", TaskVisibility.Public);
        await AddTaskAsync(friend, "Subtask", TaskVisibility.Public, parentTaskId: parent.Id);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_OwnTasks_ExcludedFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        await AddTaskAsync(owner, "My own public task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_MapsItemStatus()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        await AddTaskAsync(friend, "In progress task", TaskVisibility.Public, TaskItemStatus.InProgress);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        item.ItemStatus.ShouldBe(TaskItemStatus.InProgress);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_OrdersByMostRecentFirst()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var first = await AddTaskAsync(friend, "First task", TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var second = await AddTaskAsync(friend, "Second task", TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var third = await AddTaskAsync(friend, "Third task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.Select(item => item.Id).ShouldBe(new[] { third.Id, second.Id, first.Id });
        feed.Items.Select(item => item.CreatedAt)
            .ShouldBe(new[] { third.CreatedAt, second.CreatedAt, first.CreatedAt });
    }

    [Fact]
    public async Task GetFriendsFeedAsync_Pagination_ReturnsRequestedPageAndTotalPages()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        for (var i = 0; i < 5; i++)
        {
            await AddTaskAsync(friend, $"Task {i}", TaskVisibility.Public);
        }

        var firstPage = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 2);
        var secondPage = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 2, limit: 2);

        firstPage.Items.Count.ShouldBe(2);
        firstPage.Page.ShouldBe(1);
        firstPage.TotalPages.ShouldBe(3);

        secondPage.Items.Count.ShouldBe(2);
        secondPage.Page.ShouldBe(2);
        firstPage.Items.Select(item => item.Id)
            .ShouldNotContain(id => secondPage.Items.Select(second => second.Id).Contains(id));
    }

    [Fact]
    public async Task GetFriendsFeedAsync_LimitAboveMax_IsClampedTo50()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        for (var i = 0; i < 3; i++)
        {
            await AddTaskAsync(friend, $"Task {i}", TaskVisibility.Public);
        }

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 1000);

        feed.Items.Count.ShouldBe(3);
        feed.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_PageBelowOne_IsClampedToFirstPage()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        await AddTaskAsync(friend, "Only task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 0, limit: 20);

        feed.Page.ShouldBe(1);
        feed.Items.ShouldHaveSingleItem();
    }


    [Fact]
    public async Task GetFriendsFeedAsync_ViewedFollowingTask_ExcludedFromPrimaryTier()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var seen = await AddTaskAsync(friend, "Seen task", TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var unseen = await AddTaskAsync(friend, "Unseen task", TaskVisibility.Public);

        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { seen.Id });

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 1);

        var item = feed.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(unseen.Id);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_ViewedFollowingTask_ReappearsOnlyAfterUnseenExhausted()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var seen = await AddTaskAsync(friend, "Seen task", TaskVisibility.Public);
        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var unseen = await AddTaskAsync(friend, "Unseen task", TaskVisibility.Public);

        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { seen.Id });

        var firstPage = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 1);
        var secondPage = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 2, limit: 1);

        firstPage.Items.ShouldHaveSingleItem().Id.ShouldBe(unseen.Id);
        secondPage.Items.ShouldHaveSingleItem().Id.ShouldBe(seen.Id);
    }

    [Fact]
    public async Task GetFriendsFeedAsync_ViewedFollowingTask_UpdatedAfterView_ReturnsToPrimaryTier()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var task = await AddTaskAsync(friend, "Task", TaskVisibility.Public);
        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { task.Id });

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            var entity = await context.TaskItems.SingleAsync(t => t.Id == task.Id);
            entity.UpdatedAt = FakeTimeProvider.GetUtcNow();
            await context.SaveChangesAsync();
        }

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(task.Id);
        item.IsUpdated.ShouldBeTrue();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_SeenFollowingTask_OrderedAfterDiscoveryPool()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        await MakeFriendsAsync(owner, friend);

        var seenFollowing = await AddTaskAsync(friend, "Seen following task", TaskVisibility.Public);
        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { seenFollowing.Id });

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var discovery = await AddTaskAsync(stranger, "Discovery task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        feed.Items.Select(i => i.Id).ShouldBe(new[] { discovery.Id, seenFollowing.Id });
    }

    [Fact]
    public async Task GetFriendsFeedAsync_TotalPages_AccountsForAllThreeTiers()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var seen = await AddTaskAsync(friend, "Seen task", TaskVisibility.Public);
        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { seen.Id });
        await AddTaskAsync(friend, "Unseen task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 1);

        feed.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task MarkTasksAsViewedAsync_NewTask_CreatesTaskView()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        var task = await AddTaskAsync(friend, "Task", TaskVisibility.Public);

        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { task.Id });

        var view = await GetTaskViewAsync(owner.Id, task.Id);
        view.ShouldNotBeNull();
        view!.ViewedAt.ShouldBe(FakeTimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task MarkTasksAsViewedAsync_ExistingView_UpdatesViewedAt()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        var task = await AddTaskAsync(friend, "Task", TaskVisibility.Public);

        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { task.Id });
        var firstViewedAt = (await GetTaskViewAsync(owner.Id, task.Id))!.ViewedAt;

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(5));
        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { task.Id });

        var view = await GetTaskViewAsync(owner.Id, task.Id);
        view.ShouldNotBeNull();
        view!.ViewedAt.ShouldBeGreaterThan(firstViewedAt);
        view.ViewedAt.ShouldBe(FakeTimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task MarkTasksAsViewedAsync_DuplicateIdsInInput_OnlyCreatesOneView()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        var task = await AddTaskAsync(friend, "Task", TaskVisibility.Public);

        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, new[] { task.Id, task.Id, task.Id });

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var count = await context.TaskViews.CountAsync(v => v.UserId == owner.Id && v.TaskItemId == task.Id);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task MarkTasksAsViewedAsync_EmptyList_DoesNothing()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");

        await ServiceUnderTest.MarkTasksAsViewedAsync(owner.Id, Array.Empty<int>());

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var count = await context.TaskViews.CountAsync(v => v.UserId == owner.Id);
        count.ShouldBe(0);
    }
    [Fact]
    public async Task GetFriendsFeedAsync_TaskHasReaction_IncludesReactionInFeedItem()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var task = await AddTaskAsync(friend, "Reacted task", TaskVisibility.Public);
        var reaction = await AddReactionToDatabaseAsync("thumbsup");
        await AddTaskReactionAsync(task, reaction, owner);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        var returnedReaction = item.Reactions.ShouldHaveSingleItem();
        returnedReaction.Reaction.Name.ShouldBe("thumbsup");
        returnedReaction.Count.ShouldBe(1);
        returnedReaction.ReactedByCurrentUser.ShouldBeTrue();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_TaskHasNoReactions_ReturnsEmptyReactionsList()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        await AddTaskAsync(friend, "No reactions task", TaskVisibility.Public);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        item.Reactions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_ReactionFromDifferentUser_ReactedByCurrentUserFalse()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var task = await AddTaskAsync(friend, "Reacted task", TaskVisibility.Public);
        var reaction = await AddReactionToDatabaseAsync("heart");
        await AddTaskReactionAsync(task, reaction, friend); // friend reacted, not owner

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        var returnedReaction = item.Reactions.ShouldHaveSingleItem();
        returnedReaction.Count.ShouldBe(1);
        returnedReaction.ReactedByCurrentUser.ShouldBeFalse();
    }

    [Fact]
    public async Task GetFriendsFeedAsync_TaskHasMultipleDistinctReactions_GroupedSeparately()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        await MakeFriendsAsync(owner, friend);

        var task = await AddTaskAsync(friend, "Popular task", TaskVisibility.Public);
        var thumbsUp = await AddReactionToDatabaseAsync("thumbsup");
        var heart = await AddReactionToDatabaseAsync("heart");
        await AddTaskReactionAsync(task, thumbsUp, owner);
        await AddTaskReactionAsync(task, heart, friend);

        var feed = await ServiceUnderTest.GetFriendsFeedAsync(owner.Id, page: 1, limit: 20);

        var item = feed.Items.ShouldHaveSingleItem();
        item.Reactions.Count.ShouldBe(2);
        item.Reactions.Select(r => r.Reaction.Name).ShouldContain("thumbsup");
        item.Reactions.Select(r => r.Reaction.Name).ShouldContain("heart");
    }
}
