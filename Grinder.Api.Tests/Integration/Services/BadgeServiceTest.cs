using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Integration.Services;

public class BadgeServiceTest : BaseIntegrationTestFixture
{
    private int _userId;

    private IBadgeService ServiceUnderTest =>
        ServiceProvider.GetRequiredService<IBadgeService>();

    public BadgeServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await AddUserToDatabaseAsync("test@test.com", "Tester");
        await SeedBadgesAsync();
    }

    private async Task<int> AddUserToDatabaseAsync(string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "hash",
            Country = "NZ",
            IsVerified = true
        };
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedBadgesAsync()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Badges.AddRange(
            new Badge
            {
                Name = "The Grinder",
                Icon = "grinder",
                Description = "Awarded for having 10 tasks in Done status.",
                CriteriaKey = "TasksDone",
                Threshold = 10
            },
            new Badge
            {
                Name = "First Steps",
                Icon = "rocket",
                Description = "Complete your first task.",
                CriteriaKey = "TasksDone",
                Threshold = 1
            }
        );
        await context.SaveChangesAsync();
    }

    private async Task AddTasksAsync(int count, TaskItemStatus status = TaskItemStatus.Todo)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        for (int i = 0; i < count; i++)
        {
            context.TaskItems.Add(new TaskItem
            {
                UserId = _userId,
                Title = $"Task {i + 1}",
                DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7)),
                ItemStatus = status
            });
        }
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllBadgesForUser_NoBadgesEarned_AllShowAsLocked()
    {
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);

        var badgeList = badges.ToList();
        badgeList.Count.ShouldBe(2);
        badgeList.ShouldAllBe(b => !b.IsUnlocked);
        badgeList.ShouldAllBe(b => b.EarnedAt == null);
    }

    [Fact]
    public async Task GetAllBadgesForUser_BadgeEarned_ShowsAsUnlockedWithDate()
    {
        // Arrange: earn "First Steps" by completing 1 task
        await AddTasksAsync(1, TaskItemStatus.Done);
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Act
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);

        // Assert
        var firstSteps = badges.First(b => b.Name == "First Steps");
        firstSteps.IsUnlocked.ShouldBeTrue();
        firstSteps.EarnedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAllBadgesForUser_ReturnsAllBadgeFields()
    {
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);

        var grinder = badges.First(b => b.Name == "The Grinder");
        grinder.Icon.ShouldBe("grinder");
        grinder.Description.ShouldBe("Awarded for having 10 tasks in Done status.");
        grinder.IsUnlocked.ShouldBeFalse();
        grinder.EarnedAt.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateBadges_ThresholdNotMet_NoBadgeAwarded()
    {
        // Arrange: add 9 done tasks (threshold is 10 for "The Grinder")
        await AddTasksAsync(9, TaskItemStatus.Done);

        // Act
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Assert
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);
        var grinder = badges.First(b => b.Name == "The Grinder");
        grinder.IsUnlocked.ShouldBeFalse();
    }

    [Fact]
    public async Task EvaluateBadges_ThresholdMet_BadgeAwarded()
    {
        // Arrange: add 10 done tasks
        await AddTasksAsync(10, TaskItemStatus.Done);

        // Act
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Assert
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);
        var grinder = badges.First(b => b.Name == "The Grinder");
        grinder.IsUnlocked.ShouldBeTrue();
        grinder.EarnedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateBadges_AlreadyEarned_DoesNotDuplicate()
    {
        // Arrange: earn the badge
        await AddTasksAsync(10, TaskItemStatus.Done);
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Act: evaluate again
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Assert: still only two UserBadge rows (First Steps + The Grinder)
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var count = await context.UserBadges.CountAsync(ub => ub.UserId == _userId);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateBadges_MultipleThresholdsMet_AwardsMultipleBadges()
    {
        // Arrange: 10 done tasks meets both "First Steps" (threshold 1) and "The Grinder" (threshold 10)
        await AddTasksAsync(10, TaskItemStatus.Done);

        // Act
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Assert
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);
        badges.Count(b => b.IsUnlocked).ShouldBe(2);
    }

    [Fact]
    public async Task EvaluateBadges_OnlyTodoTasks_NoBadgeAwarded()
    {
        // Arrange: add tasks that are not Done
        await AddTasksAsync(10, TaskItemStatus.Todo);

        // Act
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        // Assert
        var badges = await ServiceUnderTest.GetAllBadgesForUserAsync(_userId);
        badges.ShouldAllBe(b => !b.IsUnlocked);
    }

    [Fact]
    public async Task SelectBadge_BadgeNotEarned_ReturnsFalse()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var badge = await context.Badges.FirstAsync();

        var result = await ServiceUnderTest.SelectBadgeAsync(_userId, badge.Id);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task SelectBadge_BadgeEarned_ReturnsTrueAndSetsOnUser()
    {
        // Arrange: earn a badge
        await AddTasksAsync(1, TaskItemStatus.Done);
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var earnedBadge = await context.UserBadges.FirstAsync(ub => ub.UserId == _userId);

        // Act
        var result = await ServiceUnderTest.SelectBadgeAsync(_userId, earnedBadge.BadgeId);

        // Assert
        result.ShouldBeTrue();
        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var user = await verifyContext.Users.FirstAsync(u => u.Id == _userId);
        user.SelectedBadgeId.ShouldBe(earnedBadge.BadgeId);
    }

    [Fact]
    public async Task SelectBadge_NullBadgeId_ClearsSelection()
    {
        // Arrange: earn and select a badge first
        await AddTasksAsync(1, TaskItemStatus.Done);
        await ServiceUnderTest.EvaluateBadgesAsync(_userId);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var earnedBadge = await context.UserBadges.FirstAsync(ub => ub.UserId == _userId);
        await ServiceUnderTest.SelectBadgeAsync(_userId, earnedBadge.BadgeId);

        // Act
        var result = await ServiceUnderTest.SelectBadgeAsync(_userId, null);

        // Assert
        result.ShouldBeTrue();
        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var user = await verifyContext.Users.FirstAsync(u => u.Id == _userId);
        user.SelectedBadgeId.ShouldBeNull();
    }

    [Fact]
    public async Task SelectBadge_InvalidUserId_ReturnsFalse()
    {
        var result = await ServiceUnderTest.SelectBadgeAsync(99999, 1);

        result.ShouldBeFalse();
    }
}
