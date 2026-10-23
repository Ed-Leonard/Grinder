using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U30018Wrapped
{
    private readonly AcceptanceTestFixture _fixture;
    private WrappedStatsResponse? _stats;

    public U30018Wrapped(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task SetCurrentUserCreatedAt(DateTime createdAt)
    {
        await using var db = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await db.Users.FirstAsync(u => u.Id == _fixture.CurrentUserId);
        user.CreatedAt = createdAt;
        await db.SaveChangesAsync();
    }

    private async Task AddTaskAsync(TaskItemStatus status, DateTimeOffset? completedAt)
    {
        var now = _fixture.AcceptanceTestNow;
        await using var db = await _fixture.DbContextFactory.CreateDbContextAsync();
        db.TaskItems.Add(new TaskItem
        {
            UserId = _fixture.CurrentUserId!.Value,
            Title = "Task",
            DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
            ItemStatus = status,
            CreatedAt = now.AddMonths(-6),
            UpdatedAt = now,
            CompletedAt = completedAt
        });
        await db.SaveChangesAsync();
    }

    private async Task<WrappedConstantEligibilityResponse> ReadEligibilityAsync()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await _fixture.LastResponse.Content.ReadFromJsonAsync<WrappedConstantEligibilityResponse>();
        body.ShouldNotBeNull();
        return body;
    }

    private async Task<WrappedStatsResponse> ReadStatsAsync()
    {
        if (_stats is not null)
        {
            return _stats;
        }

        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
        _stats = await _fixture.LastResponse.Content.ReadFromJsonAsync<WrappedStatsResponse>();
        _stats.ShouldNotBeNull();
        return _stats;
    }

    [Given(@"my account is more than 2 months old")]
    public async Task GivenMyAccountIsMoreThan2MonthsOld()
    {
        await SetCurrentUserCreatedAt(_fixture.AcceptanceTestNow.UtcDateTime.AddMonths(-3));
    }

    [Given(@"my account is less than 2 months old")]
    public async Task GivenMyAccountIsLessThan2MonthsOld()
    {
        await SetCurrentUserCreatedAt(_fixture.AcceptanceTestNow.UtcDateTime.AddDays(-7));
    }

    [Given(@"I have completed at least one task in the current calendar year")]
    public async Task GivenIHaveCompletedAtLeastOneTaskThisCalendarYear()
    {
        await AddTaskAsync(
            TaskItemStatus.Done,
            completedAt: _fixture.AcceptanceTestNow.AddDays(-10));
    }

    [Given(@"I have not completed a task in the current calendar year")]
    public async Task GivenIHaveNotCompletedATaskThisCalendarYear()
    {
        var lastYear = _fixture.AcceptanceTestNow.Year - 1;
        await AddTaskAsync(
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(lastYear, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Given(@"I completed a task in (\d+)")]
    public async Task GivenICompletedATaskInYear(int year)
    {
        await AddTaskAsync(
            TaskItemStatus.Done,
            completedAt: new DateTimeOffset(year, 5, 15, 0, 0, 0, TimeSpan.Zero));
    }

    [Given(@"I have not completed a task in the previous calendar year")]
    public void GivenIHaveNotCompletedATaskInThePreviousCalendarYear()
    {
        // Intentionally left empty — no task added for the previous year.
    }

    [Given(@"I am eligible for Wrapped")]
    public async Task GivenIAmEligibleForWrapped()
    {
        await GivenMyAccountIsMoreThan2MonthsOld();
        await GivenIHaveCompletedAtLeastOneTaskThisCalendarYear();
    }

    [Given(@"I have Wrapped slide data")]
    public async Task GivenIHaveWrappedSlideData()
    {
        var now = _fixture.AcceptanceTestNow;
        await using var db = await _fixture.DbContextFactory.CreateDbContextAsync();
        var userId = _fixture.CurrentUserId!.Value;

        var popular = new TaskItem
        {
            UserId = userId,
            Title = "Popular",
            DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            CreatedAt = now.AddMonths(-1),
            UpdatedAt = now
        };
        db.TaskItems.Add(popular);
        await db.SaveChangesAsync();

        var reaction = new Reaction
        {
            Name = Guid.NewGuid().ToString("N"),
            FileName = Guid.NewGuid().ToString("N") + ".png"
        };
        db.Reactions.Add(reaction);
        await db.SaveChangesAsync();
        db.TaskReactions.Add(new TaskReaction
        {
            TaskItemId = popular.Id,
            ReactionId = reaction.Id,
            UserId = userId,
            CreatedAt = now
        });

        var comment = new Comment
        {
            TaskId = popular.Id,
            AuthorId = userId,
            Content = "Popular comment",
            CreatedAt = now.AddMonths(-1)
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
        db.Comments.Add(new Comment
        {
            TaskId = popular.Id,
            AuthorId = userId,
            Content = "reply",
            ParentCommentId = comment.Id,
            CreatedAt = now
        });

        var tasks = new List<TaskItem>();
        for (var i = 0; i < 6; i++)
        {
            var task = new TaskItem
            {
                UserId = userId,
                Title = $"t{i + 1}",
                DueDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
                ItemStatus = TaskItemStatus.Todo,
                CreatedAt = now.AddMonths(-1),
                UpdatedAt = now
            };
            db.TaskItems.Add(task);
            tasks.Add(task);
        }
        await db.SaveChangesAsync();

        async Task AddCategory(TaskItem task, string name)
        {
            var tracked = await db.TaskItems.Include(t => t.Categories).FirstAsync(t => t.Id == task.Id);
            var category = await db.Category.FirstOrDefaultAsync(c => c.Name == name)
                ?? new Category { Name = name };
            tracked.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        await AddCategory(tasks[0], "Home");
        await AddCategory(tasks[1], "Home");
        await AddCategory(tasks[2], "Home");
        await AddCategory(tasks[0], "Uni");
        await AddCategory(tasks[1], "Uni");
        await AddCategory(tasks[2], "Fitness");
        await AddCategory(tasks[3], "Shopping");
        await AddCategory(tasks[4], "Work");
        await AddCategory(tasks[5], "Personal");
    }

    [When(@"I check Wrapped eligibility")]
    public async Task WhenICheckWrappedEligibility()
    {
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync("/api/wrapped/constant-eligibility");
    }

    [Then(@"I am eligible to view the current year's Wrapped")]
    public async Task ThenIAmEligibleToViewTheCurrentYearsWrapped()
    {
        var body = await ReadEligibilityAsync();
        body.CurrentYearEligible.ShouldBeTrue();
    }

    [Then(@"I am not eligible to view the current year's Wrapped")]
    public async Task ThenIAmNotEligibleToViewTheCurrentYearsWrapped()
    {
        var body = await ReadEligibilityAsync();
        body.CurrentYearEligible.ShouldBeFalse();
    }

    [Then(@"I am eligible to view the previous year's Wrapped")]
    public async Task ThenIAmEligibleToViewThePreviousYearsWrapped()
    {
        var body = await ReadEligibilityAsync();
        body.PreviousYearEligible.ShouldBeTrue();
    }

    [Then(@"I am not eligible to view the previous year's Wrapped")]
    public async Task ThenIAmNotEligibleToViewThePreviousYearsWrapped()
    {
        var body = await ReadEligibilityAsync();
        body.PreviousYearEligible.ShouldBeFalse();
    }

    [When(@"I request my Wrapped stats")]
    public async Task WhenIRequestMyWrappedStats()
    {
        var year = _fixture.AcceptanceTestNow.Year;
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync($"/api/wrapped/stats?year={year}");
    }

    [Then(@"I see tasks started and completed with a completion percent")]
    public async Task ThenISeeTasksStartedAndCompleted()
    {
        var stats = await ReadStatsAsync();
        stats.TasksStarted.ShouldBe(8);
        stats.TasksCompleted.ShouldBe(1);
        stats.CompletionPercent.ShouldBe(12);
    }

    [Then(@"I see my most-liked task and most-liked comment")]
    public async Task ThenISeeMostLikedTaskAndComment()
    {
        var stats = await ReadStatsAsync();
        stats.MostLikedTask.ShouldNotBeNull();
        stats.MostLikedTask!.Title.ShouldBe("Popular");
        stats.MostLikedTask.ReactionCount.ShouldBe(1);
        stats.MostLikedComment.ShouldNotBeNull();
        stats.MostLikedComment!.Content.ShouldBe("Popular comment");
        stats.MostLikedComment.ReplyCount.ShouldBe(1);
    }

    [Then(@"I see my top 5 task categories")]
    public async Task ThenISeeMyTopFiveTaskCategories()
    {
        var stats = await ReadStatsAsync();
        stats.TopCategories.ShouldNotBeNull();
        stats.TopCategories!.Count.ShouldBe(5);
        stats.TopCategories.Select(c => c.Name)
            .ShouldBe(["Home", "Uni", "Fitness", "Personal", "Shopping"]);
    }

}