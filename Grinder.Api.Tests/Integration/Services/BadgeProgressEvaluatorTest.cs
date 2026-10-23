using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public sealed class BadgeProgressEvaluatorTest : IDisposable
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DatabaseContext> _options;
    private bool _disposed;

    public BadgeProgressEvaluatorTest()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new DatabaseContext(_options);
        context.Database.EnsureCreated();
    }

    ~BadgeProgressEvaluatorTest()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _connection.Dispose();
        }

        _disposed = true;
    }

    private static BadgeProgressEvaluator CreateEvaluator(IFollowService followService)
    {
        return new BadgeProgressEvaluator(new FakeTimeProvider(TestNow), followService);
    }

    private async Task<int> SeedUserAsync(DatabaseContext context, string email)
    {
        var user = new User
        {
            Email = email,
            DisplayName = email,
            PasswordHash = "hash",
            Country = "NZ",
            IsVerified = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task ComputeStatsAsync_CountsEachStatFromOnlyTheRequestedUsersTasks()
    {
        int userId;
        int otherUserId;

        await using (var seedContext = new DatabaseContext(_options))
        {
            userId = await SeedUserAsync(seedContext, "target@example.com");
            otherUserId = await SeedUserAsync(seedContext, "other@example.com");

            var today = DateOnly.FromDateTime(TestNow.DateTime);
            var tomorrow = today.AddDays(1);
            var yesterday = today.AddDays(-1);

            var category = new Category { Name = "Work" };
            seedContext.Category.Add(category);

            var doneTaskA = new TaskItem { UserId = userId, Title = "Done A", DueDate = tomorrow, ItemStatus = TaskItemStatus.Done };
            var doneTaskB = new TaskItem { UserId = userId, Title = "Done B", DueDate = tomorrow, ItemStatus = TaskItemStatus.Done };
            var overdueByDate = new TaskItem { UserId = userId, Title = "Overdue by date", DueDate = yesterday, ItemStatus = TaskItemStatus.Todo };
            var overdueByTime = new TaskItem
            {
                UserId = userId,
                Title = "Overdue by time",
                DueDate = today,
                DueTime = new TimeOnly(8, 0),
                ItemStatus = TaskItemStatus.InProgress
            };
            var notOverdue = new TaskItem { UserId = userId, Title = "Not overdue", DueDate = tomorrow, ItemStatus = TaskItemStatus.Todo };
            var clip = new TaskItem { UserId = userId, Title = "Clip", DueDate = tomorrow, ItemStatus = TaskItemStatus.Todo, IsClip = true };
            var categorized = new TaskItem
            {
                UserId = userId,
                Title = "Categorized",
                DueDate = tomorrow,
                ItemStatus = TaskItemStatus.Todo,
                Categories = { category }
            };

            seedContext.TaskItems.AddRange(doneTaskA, doneTaskB, overdueByDate, overdueByTime, notOverdue, clip, categorized);
            await seedContext.SaveChangesAsync();

            var subtask = new TaskItem
            {
                UserId = userId,
                Title = "Subtask",
                DueDate = tomorrow,
                ItemStatus = TaskItemStatus.Todo,
                ParentTaskId = doneTaskA.Id
            };
            seedContext.TaskItems.Add(subtask);

            // Noise belonging to a different user - must not be counted for userId.
            seedContext.TaskItems.Add(new TaskItem
            {
                UserId = otherUserId,
                Title = "Other user's done task",
                DueDate = yesterday,
                ItemStatus = TaskItemStatus.Done,
                IsClip = true
            });

            await seedContext.SaveChangesAsync();
        }

        var followService = Substitute.For<IFollowService>();
        followService.GetFriendCountAsync(userId).Returns(3);

        var evaluator = CreateEvaluator(followService);

        await using var context = new DatabaseContext(_options);
        var stats = await evaluator.ComputeStatsAsync(context, userId);

        stats.TasksDoneCount.ShouldBe(2);
        stats.TasksCreatedCount.ShouldBe(8);
        stats.TasksOverdueCount.ShouldBe(2);
        stats.ClipsCreatedCount.ShouldBe(1);
        stats.SubtasksCreatedCount.ShouldBe(1);
        stats.TasksCategorizedCount.ShouldBe(1);
        stats.FriendsCount.ShouldBe(3);
    }

    [Fact]
    public async Task ComputeStatsAsync_NoTasks_ReturnsAllZeroCounts()
    {
        int userId;
        await using (var seedContext = new DatabaseContext(_options))
        {
            userId = await SeedUserAsync(seedContext, "empty@example.com");
        }

        var followService = Substitute.For<IFollowService>();
        followService.GetFriendCountAsync(userId).Returns(0);

        var evaluator = CreateEvaluator(followService);

        await using var context = new DatabaseContext(_options);
        var stats = await evaluator.ComputeStatsAsync(context, userId);

        stats.ShouldBe(new BadgeCriteriaStats(0, 0, 0, 0, 0, 0, 0));
    }
}
