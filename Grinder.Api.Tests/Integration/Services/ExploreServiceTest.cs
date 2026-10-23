using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class ExploreServiceTest : BaseIntegrationTestFixture
{
    private IExploreService ServiceUnderTest => ServiceProvider.GetRequiredService<IExploreService>();

    public ExploreServiceTest(WebApplicationFactory<Program> webAppFactory)
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

    private async Task SetUpdatedAtAsync(TaskItem task, DateTimeOffset updatedAt)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var entity = await context.TaskItems.SingleAsync(t => t.Id == task.Id);
        entity.UpdatedAt = updatedAt;
        await context.SaveChangesAsync();
    }

    private async Task MarkTaskAsViewedAsync(User user, TaskItem task, DateTimeOffset? viewedAt = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskViews.Add(new TaskView
        {
            UserId = user.Id,
            TaskItemId = task.Id,
            ViewedAt = viewedAt ?? FakeTimeProvider.GetUtcNow()
        });
        await context.SaveChangesAsync();
    }

    private async Task<Reaction> AddReactionToDatabaseAsync(string name = "thumbsup")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var reaction = new Reaction { Name = name, FileName = $"{name}.png" };
        context.Reactions.Add(reaction);
        await context.SaveChangesAsync();
        return reaction;
    }

    private async Task AddTaskReactionAsync(TaskItem task, Reaction reaction, User user, DateTime? createdAt = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskReactions.Add(new TaskReaction
        {
            TaskItemId = task.Id,
            ReactionId = reaction.Id,
            UserId = user.Id,
            CreatedAt = createdAt ?? FakeTimeProvider.GetUtcNow().UtcDateTime
        });
        await context.SaveChangesAsync();
    }

    private async Task AddCommentAsync(TaskItem task, User user, DateTime? createdAt = null, bool isDeleted = false)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Comments.Add(new Comment
        {
            TaskId = task.Id,
            AuthorId = user.Id,
            Content = "a comment",
            CreatedAt = createdAt ?? FakeTimeProvider.GetUtcNow().UtcDateTime,
            IsDeleted = isDeleted
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetExploreFeedAsync_NoPublicTasks_ReturnsEmptyFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.ShouldBeEmpty();
        explore.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task GetExploreFeedAsync_OwnPublicTask_ExcludedFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        await AddTaskAsync(owner, "My own public task", TaskVisibility.Public);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetExploreFeedAsync_OtherUsersPrivateTask_ExcludedFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        await AddTaskAsync(stranger, "Private task", TaskVisibility.Private);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetExploreFeedAsync_OtherUsersFriendsOnlyTask_ExcludedFromFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        await AddTaskAsync(stranger, "Friends only task", TaskVisibility.FriendsOnly);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetExploreFeedAsync_NonFollowedUsersPublicTask_IncludedInFeed()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        var task = await AddTaskAsync(stranger, "Public task", TaskVisibility.Public);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        var item = explore.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(task.Id);
        item.Title.ShouldBe("Public task");
    }

    [Fact]
    public async Task GetExploreFeedAsync_SubtaskExists_IncludesSubtaskInFeed()
    {
        // Unlike the friends feed, explore does not filter out subtasks.
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        var parent = await AddTaskAsync(stranger, "Parent task", TaskVisibility.Public);
        var subtask = await AddTaskAsync(stranger, "Subtask", TaskVisibility.Public, parentTaskId: parent.Id);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.Select(i => i.Id).ShouldContain(parent.Id);
        explore.Items.Select(i => i.Id).ShouldContain(subtask.Id);
    }

    [Fact]
    public async Task GetExploreFeedAsync_UnseenTask_OrderedBeforeSeenTask()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var seen = await AddTaskAsync(stranger, "Seen task", TaskVisibility.Public);
        await MarkTaskAsViewedAsync(owner, seen);

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var unseen = await AddTaskAsync(stranger, "Unseen task", TaskVisibility.Public);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 1);

        var item = explore.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(unseen.Id);
    }

    [Fact]
    public async Task GetExploreFeedAsync_SeenTask_ReappearsOnlyAfterUnseenExhausted()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var seen = await AddTaskAsync(stranger, "Seen task", TaskVisibility.Public);
        await MarkTaskAsViewedAsync(owner, seen);

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        var unseen = await AddTaskAsync(stranger, "Unseen task", TaskVisibility.Public);

        var firstPage = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 1);
        var secondPage = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 2, limit: 1);

        firstPage.Items.ShouldHaveSingleItem().Id.ShouldBe(unseen.Id);
        secondPage.Items.ShouldHaveSingleItem().Id.ShouldBe(seen.Id);
    }

    [Fact]
    public async Task GetExploreFeedAsync_TaskUpdatedAfterView_TreatedAsUnseenAgain()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var stillSeen = await AddTaskAsync(stranger, "Still seen task", TaskVisibility.Public);
        var updatedAfterView = await AddTaskAsync(stranger, "Updated after view task", TaskVisibility.Public);

        var viewedAt = FakeTimeProvider.GetUtcNow();
        await MarkTaskAsViewedAsync(owner, stillSeen, viewedAt);
        await MarkTaskAsViewedAsync(owner, updatedAfterView, viewedAt);

        FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
        await SetUpdatedAtAsync(updatedAfterView, FakeTimeProvider.GetUtcNow());

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 1);

        var item = explore.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(updatedAfterView.Id);
    }

    [Fact]
    public async Task GetExploreFeedAsync_TaskWithMoreRecentReactions_OrderedFirst()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        var reactor1 = await AddUserToDatabaseAsync("reactor1@example.com", "reactor1");
        var reactor2 = await AddUserToDatabaseAsync("reactor2@example.com", "reactor2");
        var reaction = await AddReactionToDatabaseAsync("thumbsup");

        // Created first (lower id) but with no engagement.
        var quiet = await AddTaskAsync(stranger, "Quiet task", TaskVisibility.Public);

        // Created second (higher id) but with two recent reactions.
        var popular = await AddTaskAsync(stranger, "Popular task", TaskVisibility.Public);
        await AddTaskReactionAsync(popular, reaction, reactor1);
        await AddTaskReactionAsync(popular, reaction, reactor2);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.Select(i => i.Id).ShouldBe(new[] { popular.Id, quiet.Id });
    }

    [Fact]
    public async Task GetExploreFeedAsync_ReactionOlderThan24Hours_NotCountedTowardScore()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        var reactor = await AddUserToDatabaseAsync("reactor@example.com", "reactor");
        var reaction = await AddReactionToDatabaseAsync("thumbsup");

        // Created second (higher id), so it would win a plain id tie-break, but its only
        // reaction will age out of the 24h window before the feed is fetched.
        var quiet = await AddTaskAsync(stranger, "Quiet task", TaskVisibility.Public);
        var staleReacted = await AddTaskAsync(stranger, "Stale reaction task", TaskVisibility.Public);
        await AddTaskReactionAsync(staleReacted, reaction, reactor);

        FakeTimeProvider.Advance(TimeSpan.FromHours(25));

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        // Both tasks have an effective score of 0, so plain id-descending tie-break applies.
        explore.Items.Select(i => i.Id).ShouldBe(new[] { staleReacted.Id, quiet.Id });
    }

    [Fact]
    public async Task GetExploreFeedAsync_RecentComments_CountTowardScore()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        var commenter = await AddUserToDatabaseAsync("commenter@example.com", "commenter");

        var quiet = await AddTaskAsync(stranger, "Quiet task", TaskVisibility.Public);
        var commented = await AddTaskAsync(stranger, "Commented task", TaskVisibility.Public);
        await AddCommentAsync(commented, commenter);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.Select(i => i.Id).ShouldBe(new[] { commented.Id, quiet.Id });
    }

    [Fact]
    public async Task GetExploreFeedAsync_DeletedComments_NotCountedTowardScore()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        var commenter = await AddUserToDatabaseAsync("commenter@example.com", "commenter");

        var quiet = await AddTaskAsync(stranger, "Quiet task", TaskVisibility.Public);
        var deletedCommentTask = await AddTaskAsync(stranger, "Deleted comment task", TaskVisibility.Public);
        await AddCommentAsync(deletedCommentTask, commenter, isDeleted: true);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        // Both tasks have an effective score of 0, so plain id-descending tie-break applies.
        explore.Items.Select(i => i.Id).ShouldBe(new[] { deletedCommentTask.Id, quiet.Id });
    }

    [Fact]
    public async Task GetExploreFeedAsync_EqualScores_TieBreaksByIdDescending()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        var first = await AddTaskAsync(stranger, "First task", TaskVisibility.Public);
        var second = await AddTaskAsync(stranger, "Second task", TaskVisibility.Public);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        explore.Items.Select(i => i.Id).ShouldBe(new[] { second.Id, first.Id });
    }

    [Fact]
    public async Task GetExploreFeedAsync_Pagination_ReturnsRequestedPageAndTotalPages()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        for (var i = 0; i < 5; i++)
        {
            await AddTaskAsync(stranger, $"Task {i}", TaskVisibility.Public);
        }

        var firstPage = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 2);
        var secondPage = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 2, limit: 2);

        firstPage.Items.Count.ShouldBe(2);
        firstPage.Page.ShouldBe(1);
        firstPage.TotalPages.ShouldBe(3);

        secondPage.Items.Count.ShouldBe(2);
        secondPage.Page.ShouldBe(2);
        firstPage.Items.Select(i => i.Id)
            .ShouldNotContain(id => secondPage.Items.Select(second => second.Id).Contains(id));
    }

    [Fact]
    public async Task GetExploreFeedAsync_LimitAboveMax_IsClampedTo50()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        for (var i = 0; i < 3; i++)
        {
            await AddTaskAsync(stranger, $"Task {i}", TaskVisibility.Public);
        }

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 1000);

        explore.Items.Count.ShouldBe(3);
        explore.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task GetExploreFeedAsync_LimitBelowMin_IsClampedTo1()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        for (var i = 0; i < 3; i++)
        {
            await AddTaskAsync(stranger, $"Task {i}", TaskVisibility.Public);
        }

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 0);

        explore.Items.Count.ShouldBe(1);
        explore.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task GetExploreFeedAsync_PageBelowOne_IsClampedToFirstPage()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        await AddTaskAsync(stranger, "Only task", TaskVisibility.Public);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 0, limit: 20);

        explore.Page.ShouldBe(1);
        explore.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetExploreFeedAsync_NoAttachments_ReturnsEmptyAttachmentsList()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var stranger = await AddUserToDatabaseAsync("stranger@example.com", "stranger");
        await AddTaskAsync(stranger, "Task without attachments", TaskVisibility.Public);

        var explore = await ServiceUnderTest.GetExploreFeedAsync(owner.Id, page: 1, limit: 20);

        var item = explore.Items.ShouldHaveSingleItem();
        item.TaskAttachments.ShouldBeEmpty();
    }
}
