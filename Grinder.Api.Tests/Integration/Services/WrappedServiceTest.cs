using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class WrappedServiceTest : BaseIntegrationTestFixture
{
    private IWrappedService ServiceUnderTest => new WrappedService(DbContextFactory, FakeTimeProvider);

    public WrappedServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Mid-December UTC so GetLocalNow() is still December regardless of server timezone.
        FakeTimeProvider.SetUtcNow(new DateTimeOffset(2026, 12, 4, 12, 0, 0, TimeSpan.Zero));
    }

    private async Task<User> AddUserToDatabaseAsync(DateTimeOffset createdAt)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = new User
        {
            Email = Guid.NewGuid() + "@example.com",
            DisplayName = "Test User",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            IsVerified = true,
            CreatedAt = createdAt.UtcDateTime
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<TaskItem> AddTaskAsync(
        User user,
        TaskItemStatus status,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? updatedAt = null,
        DateTimeOffset? createdAt = null,
        DateOnly? dueDate = null,
        string title = "Task")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var now = FakeTimeProvider.GetUtcNow();
        var task = new TaskItem
        {
            UserId = user.Id,
            Title = title,
            DueDate = dueDate ?? DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
            ItemStatus = status,
            CreatedAt = createdAt ?? now.AddMonths(-6),
            UpdatedAt = updatedAt ?? now,
            CompletedAt = completedAt,
        };
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private async Task AddTaskReactionAsync(TaskItem task, User reactor)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var reaction = new Reaction
        {
            Name = Guid.NewGuid().ToString("N"),
            FileName = Guid.NewGuid().ToString("N") + ".png"
        };
        context.Reactions.Add(reaction);
        await context.SaveChangesAsync();

        context.TaskReactions.Add(new TaskReaction
        {
            TaskItemId = task.Id,
            ReactionId = reaction.Id,
            UserId = reactor.Id,
            CreatedAt = FakeTimeProvider.GetUtcNow()
        });
        await context.SaveChangesAsync();
    }

    private async Task<Comment> AddCommentAsync(
        TaskItem task,
        User author,
        string content,
        int? parentCommentId = null,
        DateTimeOffset? createdAt = null,
        bool isDeleted = false)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var comment = new Comment
        {
            TaskId = task.Id,
            AuthorId = author.Id,
            Content = content,
            CreatedAt = createdAt ?? FakeTimeProvider.GetUtcNow(),
            ParentCommentId = parentCommentId,
            IsDeleted = isDeleted

        };
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
        return comment;
    }

    private async Task AddCategoryToTaskAsync(TaskItem task, string name)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var tracked = await context.TaskItems
            .Include(t => t.Categories)
            .FirstAsync(t => t.Id == task.Id);

        var category = await context.Category.FirstOrDefaultAsync(c => c.Name == name)
            ?? new Category { Name = name };

        tracked.Categories.Add(category);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_CompletedTaskThisYear_ReturnsCurrentYear()
    {
        var user = await AddUserToDatabaseAsync(FakeTimeProvider.GetUtcNow().AddMonths(-6));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: FakeTimeProvider.GetUtcNow().AddDays(-10));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBe(2026);
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_CompletedTaskEarlyIn2026Window_ReturnsCurrentYear()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBe(2026);
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_TaskCompletedBeforeWindowStart_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_TaskCompletedAtWindowStart_ReturnsCurrentYear()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBe(2026);
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_AccountCreatedOnCutoff_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: FakeTimeProvider.GetUtcNow().AddDays(-2));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_AccountCreatedJustBeforeCutoff_ReturnsCurrentYear()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBe(2026);
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_ReopenedTask_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(FakeTimeProvider.GetUtcNow().AddMonths(-6));
        await AddTaskAsync(user, TaskItemStatus.Todo, completedAt: null, updatedAt: FakeTimeProvider.GetUtcNow());

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentEligibleYearAsync_AccountLessThan2MonthsOld_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(FakeTimeProvider.GetUtcNow().AddDays(-7));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: FakeTimeProvider.GetUtcNow().AddDays(-2));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);

        currentYear.ShouldBeNull();
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_NoCompletedTasks_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(FakeTimeProvider.GetUtcNow().AddMonths(-6));
        await AddTaskAsync(user, TaskItemStatus.Todo, completedAt: null);
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBeNull();
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_CompletedTaskThisYear_ReturnsCurrentYear()
    {
        var user = await AddUserToDatabaseAsync(FakeTimeProvider.GetUtcNow().AddMonths(-6));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: FakeTimeProvider.GetUtcNow().AddDays(-10));
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBe(2026);
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_CompletedTaskEarlyInWindow_ReturnsCurrentYear()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero));
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBe(2026);
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_MultipleYearsOfWrappeds_ReturnsMostRecent()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2025, 5, 15, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero));
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBe(2026);
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_OnlyEligibleForPastYear_ReturnsThatYear()
    {
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2021, 6, 15, 0, 0, 0, TimeSpan.Zero));

        var currentYear = await ServiceUnderTest.GetCurrentEligibleYearAsync(user.Id);
        var mostRecent = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);

        currentYear.ShouldBeNull();
        mostRecent.ShouldBe(2021);
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_AccountLessThan2MonthsOld_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(FakeTimeProvider.GetUtcNow().AddDays(-7));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: FakeTimeProvider.GetUtcNow().AddDays(-2));
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBeNull();
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_BeforeDecember_ReturnsPreviousYear()
    {
        FakeTimeProvider.SetUtcNow(new DateTimeOffset(2027, 9, 15, 12, 0, 0, TimeSpan.Zero));
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero));
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBe(2026);
    }

    [Fact]
    public async Task GetMostRecentEligibleYearAsync_BeforeDecember_CurrentYearNotYetAvailable()
    {
        FakeTimeProvider.SetUtcNow(new DateTimeOffset(2027, 9, 15, 12, 0, 0, TimeSpan.Zero));
        var user = await AddUserToDatabaseAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var year = await ServiceUnderTest.GetMostRecentEligibleYearAsync(user.Id);
        year.ShouldBeNull();
    }

    [Fact]
    public async Task GetStatsAsync_FourStartedTwoCompleted_ReturnsFiftyPercent()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-10), createdAt: now.AddMonths(-1));
        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-5), createdAt: now.AddMonths(-2));
        await AddTaskAsync(user, TaskItemStatus.InProgress, completedAt: now.AddDays(-1), createdAt: now.AddMonths(-3));
        await AddTaskAsync(user, TaskItemStatus.Todo, completedAt: now.AddDays(-67), createdAt: now.AddMonths(-4));

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.TasksStarted.ShouldBe(4);
        stats.TasksCompleted.ShouldBe(2);
        stats.CompletionPercent.ShouldBe(50);
    }

    [Fact]
    public async Task GetStatsAsync_PicksMostReactedTaskAndMostRepliedComment()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        var other = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-10), createdAt: now.AddMonths(-1));
        var popularTask = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Popular");
        var quietTask = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Quiet");

        await AddTaskReactionAsync(popularTask, other);
        await AddTaskReactionAsync(popularTask, other);
        await AddTaskReactionAsync(quietTask, other);

        var popularComment = await AddCommentAsync(popularTask, user, "Popular comment", createdAt: now.AddMonths(-1));
        var quietComment = await AddCommentAsync(popularTask, user, "Quiet comment", createdAt: now.AddMonths(-1));
        await AddCommentAsync(popularTask, other, "reply 1", parentCommentId: popularComment.Id);
        await AddCommentAsync(popularTask, other, "reply 2", parentCommentId: popularComment.Id);
        await AddCommentAsync(popularTask, other, "reply", parentCommentId: quietComment.Id);

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.MostLikedTask.ShouldNotBeNull();
        stats.MostLikedTask!.Title.ShouldBe("Popular");
        stats.MostLikedTask.ReactionCount.ShouldBe(2);

        stats.MostLikedComment.ShouldNotBeNull();
        stats.MostLikedComment!.Content.ShouldBe("Popular comment");
        stats.MostLikedComment.ReplyCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsTopFiveCategoriesByTaskCount()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-10), createdAt: now.AddMonths(-1));
        var t1 = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "t1");
        var t2 = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "t2");
        var t3 = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "t3");
        var t4 = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "t4");
        var t5 = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "t5");
        var t6 = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "t6");

        await AddCategoryToTaskAsync(t1, "Home");
        await AddCategoryToTaskAsync(t2, "Home");
        await AddCategoryToTaskAsync(t3, "Home");
        await AddCategoryToTaskAsync(t1, "Uni");
        await AddCategoryToTaskAsync(t2, "Uni");
        await AddCategoryToTaskAsync(t3, "Fitness");
        await AddCategoryToTaskAsync(t4, "Shopping");
        await AddCategoryToTaskAsync(t5, "Work");
        await AddCategoryToTaskAsync(t6, "Personal");

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.TopCategories.ShouldNotBeNull();
        stats.TopCategories!.Select(c => c.Name).ShouldBe(["Home", "Uni", "Fitness", "Personal", "Shopping"]);
        stats.TopCategories[0].TaskCount.ShouldBe(3);
        stats.TopCategories[1].TaskCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetStatsAsync_IgnoresOtherUsersData()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        var other = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-10), createdAt: now.AddMonths(-1));
        var mine = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Mine");
        var theirs = await AddTaskAsync(other, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Theirs");

        await AddTaskReactionAsync(mine, other);
        await AddTaskReactionAsync(theirs, user);
        await AddTaskReactionAsync(theirs, other);
        await AddTaskReactionAsync(theirs, other);

        var myComment = await AddCommentAsync(mine, user, "Mine comment", createdAt: now.AddMonths(-1));
        var theirComment = await AddCommentAsync(theirs, other, "Theirs comment", createdAt: now.AddMonths(-1));
        await AddCommentAsync(mine, other, "reply", parentCommentId: myComment.Id);
        await AddCommentAsync(theirs, user, "r1", parentCommentId: theirComment.Id);
        await AddCommentAsync(theirs, user, "r2", parentCommentId: theirComment.Id);
        await AddCommentAsync(theirs, user, "r3", parentCommentId: theirComment.Id);

        await AddCategoryToTaskAsync(mine, "Uni");
        await AddCategoryToTaskAsync(theirs, "Home");
        await AddCategoryToTaskAsync(theirs, "Home");

        var extra = await AddTaskAsync(other, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Theirs 2");
        await AddCategoryToTaskAsync(extra, "Home");

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.MostLikedTask.ShouldNotBeNull();
        stats.MostLikedTask!.Title.ShouldBe("Mine");
        stats.MostLikedTask.ReactionCount.ShouldBe(1);

        stats.MostLikedComment.ShouldNotBeNull();
        stats.MostLikedComment!.Content.ShouldBe("Mine comment");
        stats.MostLikedComment.ReplyCount.ShouldBe(1);

        stats.TopCategories.ShouldNotBeNull();
        stats.TopCategories!.Select(c => c.Name).ShouldBe(["Uni"]);
        stats.TopCategories[0].TaskCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsAsync_IgnoresLastYearsData()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var lastYear = now.AddYears(-1);
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        var other = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        var oldTask = await AddTaskAsync(user, TaskItemStatus.Done, completedAt: lastYear, createdAt: lastYear, title: "Old popular");
        var newTask = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "New quiet");

        await AddTaskReactionAsync(oldTask, other);
        await AddTaskReactionAsync(oldTask, other);
        await AddTaskReactionAsync(newTask, other);

        var oldComment = await AddCommentAsync(oldTask, user, "Old comment", createdAt: lastYear);
        var newComment = await AddCommentAsync(newTask, user, "New comment", createdAt: now.AddMonths(-1));
        await AddCommentAsync(oldTask, other, "old 1", parentCommentId: oldComment.Id, createdAt: lastYear);
        await AddCommentAsync(oldTask, other, "old 2", parentCommentId: oldComment.Id, createdAt: lastYear);
        await AddCommentAsync(newTask, other, "new 1", parentCommentId: newComment.Id);

        await AddCategoryToTaskAsync(oldTask, "Home");
        await AddCategoryToTaskAsync(newTask, "Uni");

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.TasksStarted.ShouldBe(1);
        stats.TasksCompleted.ShouldBe(0);

        stats.MostLikedTask.ShouldNotBeNull();
        stats.MostLikedTask!.Title.ShouldBe("New quiet");
        stats.MostLikedTask.ReactionCount.ShouldBe(1);

        stats.MostLikedComment.ShouldNotBeNull();
        stats.MostLikedComment!.Content.ShouldBe("New comment");
        stats.MostLikedComment.ReplyCount.ShouldBe(1);

        stats.TopCategories.ShouldNotBeNull();
        stats.TopCategories!.Select(c => c.Name).ShouldBe(["Uni"]);
    }

    [Fact]
    public async Task GetStatsAsync_NoReactionsCommentsOrCategories_ReturnsEmptySlideFields()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Plain");

        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-10), createdAt: now.AddMonths(-1));
        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.MostLikedTask.ShouldBeNull();
        stats.MostLikedComment.ShouldBeNull();
        stats.TopCategories.ShouldNotBeNull();
        stats.TopCategories.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetStatsAsync_IgnoresDeletedComments()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        var other = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        var task = await AddTaskAsync(user, TaskItemStatus.Todo, createdAt: now.AddMonths(-1), title: "Task");

        await AddTaskAsync(user, TaskItemStatus.Done, completedAt: now.AddDays(-10), createdAt: now.AddMonths(-1));
        var deleted = await AddCommentAsync(task, user, "Deleted popular", createdAt: now.AddMonths(-1), isDeleted: true);
        var live = await AddCommentAsync(task, user, "Live quiet", createdAt: now.AddMonths(-1));
        await AddCommentAsync(task, other, "d1", parentCommentId: deleted.Id);
        await AddCommentAsync(task, other, "d2", parentCommentId: deleted.Id);
        await AddCommentAsync(task, other, "d3", parentCommentId: deleted.Id);
        await AddCommentAsync(task, other, "live reply", parentCommentId: live.Id);

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.MostLikedComment.ShouldNotBeNull();
        stats.MostLikedComment!.Content.ShouldBe("Live quiet");
        stats.MostLikedComment.ReplyCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsAsync_ZeroStarted_ReturnsZeroPercent()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.TasksStarted.ShouldBe(0);
        stats.TasksCompleted.ShouldBe(0);
        stats.CompletionPercent.ShouldBe(0);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsExtraPersonalStats()
    {
        var now = FakeTimeProvider.GetUtcNow();
        var user = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));
        var other = await AddUserToDatabaseAsync(now.UtcDateTime.AddMonths(-6));

        var januaryTask = await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            createdAt: new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        var februaryTask = await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(2026, 2, 15, 12, 0, 0, TimeSpan.Zero),
            createdAt: new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero));
        var anotherFebruaryTask = await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero),
            createdAt: new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero));
        await AddTaskAsync(
            user,
            TaskItemStatus.Todo,
            createdAt: now.AddMonths(-1),
            dueDate: DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)));
        await AddTaskAsync(
            user,
            TaskItemStatus.Done,
            completedAt: now.AddMonths(-1),
            createdAt: now.AddMonths(-2),
            dueDate: DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)));
        await AddTaskAsync(
            user,
            TaskItemStatus.Todo,
            createdAt: now,
            title: "Future due task");
        await AddTaskAsync(
            user,
            TaskItemStatus.Todo,
            createdAt: now.AddYears(-1),
            dueDate: DateOnly.FromDateTime(now.UtcDateTime.AddYears(-1)));

        await AddCommentAsync(januaryTask, user, "Written comment", createdAt: now.AddMonths(-1));
        await AddCommentAsync(januaryTask, user, "Deleted comment", createdAt: now.AddMonths(-1), isDeleted: true);
        await AddCommentAsync(januaryTask, user, "Old comment", createdAt: now.AddYears(-1));
        await AddTaskReactionAsync(februaryTask, other);
        await AddTaskReactionAsync(anotherFebruaryTask, other);

        var stats = await ServiceUnderTest.GetWrappedStatsAsync(user.Id, now.Year);

        stats.ExtraStats.ShouldNotBeNull();
        stats.ExtraStats!.NumberOfCommentsWritten.ShouldBe(1);
        stats.ExtraStats.NumberOfReactionsReceived.ShouldBe(2);
        stats.ExtraStats.NumberOfTasksOverdue.ShouldBe(1);
        stats.ExtraStats.MostProductiveMonth.ShouldNotBeNull();
        stats.ExtraStats.MostProductiveMonth!.Month.ShouldBe("February");
        stats.ExtraStats.MostProductiveMonth.TaskCount.ShouldBe(2);
    }
}
