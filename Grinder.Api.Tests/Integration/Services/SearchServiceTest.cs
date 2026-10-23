using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class SearchServiceTest : BaseIntegrationTestFixture
{
    private ISearchService ServiceUnderTest => ServiceProvider.GetRequiredService<ISearchService>();

    public SearchServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUserToDatabaseAsync(string email = "test@example.com", string displayName = "Jane")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = TestNow.UtcDateTime
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private async Task<TaskItem> AddTaskToDatabaseAsync(
        int userId,
        string title = "Test Task",
        string? description = null,
        TaskVisibility visibility = TaskVisibility.Public,
        List<Category>? categories = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var task = new TaskItem
        {
            UserId = userId,
            Title = title,
            Description = description,
            Visibility = visibility,
            CreatedAt = TestNow.UtcDateTime,
            Categories = categories ?? new List<Category>()
        };

        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private async Task<Comment> AddComment(
    int taskId,
    int authorId,
    string content = "Test Comment",
    int? parentCommentId = null)
    {
        var comment = new Comment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
            ParentCommentId = parentCommentId
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
        return comment;
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsUsers_WhenQueryMatchesDisplayName()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        var query = "Jane";

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, query);

        result.Users.ShouldNotBeEmpty();
        result.Users.ShouldContain(u => u.DisplayName == user.DisplayName);
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsEmpty_WhenQueryDoesNotMatch()
    {
        var user = await AddUserToDatabaseAsync();
        var query = "NonExistentQuery";

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, query);

        result.Users.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsUsers_WhenQueryMatchesFuzzy()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        var query = "Jape"; // a fuzzy match for "Jane"

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, query);

        result.Users.ShouldNotBeEmpty();
        result.Users.ShouldContain(u => u.DisplayName == user.DisplayName);
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsTasks_WhenQueryMatchesTitle()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");
        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Buy groceries");

        var result = await ServiceUnderTest.GetResultsAsync(
            user.Id,
            "groceries");

        result.Tasks.ShouldContain(t => t.Title == "Buy groceries");
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsTasks_WhenQueryMatchesDescription()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");
        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Weekly shopping",
            description: "Remember to buy groceries");

        var result = await ServiceUnderTest.GetResultsAsync(
            user.Id,
            "groceries");

        result.Tasks.ShouldContain(t => t.Title == "Weekly shopping");
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsTasks_WhenQueryMatchesCategory()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        var categories = new List<Category>
    {
        new Category { Name = "University" },
        new Category { Name = "Personal" }
    };

        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Finish assignment",
            description: "Complete the report",
            categories: categories);

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "personal");

        result.Tasks.ShouldContain(t => t.Title == "Finish assignment");
    }

    [Fact]
    public async Task GetResultsAsync_OrdersSameStrengthMatches_ByInteractionCount()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var lowInteractionTask = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Weekly shopping",
            description: "Remember to buy groceries");

        var highInteractionTask = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Weekly shopping",
            description: "Remember to buy groceries");

        await AddComment(highInteractionTask.Id, user.Id, "comment");

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries");

        result.Tasks[0].Id.ShouldBe(highInteractionTask.Id);
        result.Tasks[1].Id.ShouldBe(lowInteractionTask.Id);
    }

    [Fact]
    public async Task GetResultsAsync_SameMatchType_OrdersByStrengthMatches()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var exact_match = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Week",
            description: "Remember to buy groceries");

        var close_match = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Weekly Shopping",
            description: "Remember to buy groceries");

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "Week");

        result.Tasks[0].Id.ShouldBe(exact_match.Id);
        result.Tasks[1].Id.ShouldBe(close_match.Id);
    }

    [Fact]
    public async Task GetResultsAsync_OrdersTasks_ByMatchType()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        var descriptionMatch = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Shopping",
            description: "Buy groceries");

        var titleMatch = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Groceries",
            description: "Something else");

        var categoryMatch = await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Shopping",
            description: "Something else",
            categories: new List<Category>
            {
            new Category { Name = "Groceries" }
            });

        var result = await ServiceUnderTest.GetResultsAsync(
            user.Id,
            "groceries");

        result.Tasks[0].Id.ShouldBe(titleMatch.Id);
        result.Tasks[1].Id.ShouldBe(descriptionMatch.Id);
        result.Tasks[2].Id.ShouldBe(categoryMatch.Id);
    }


    [Fact]
    public async Task GetResultsAsync_DoesNotReturnPrivateTask_FromAnotherUser()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Private groceries",
            visibility: TaskVisibility.Private);

        var result = await ServiceUnderTest.GetResultsAsync(
            user.Id,
            "groceries");

        result.Tasks.ShouldNotContain(t => t.Title == "Private groceries");
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsTasks_WhenQueryMatchesFuzzy()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Buy groceries");

        var result = await ServiceUnderTest.GetResultsAsync(
            user.Id,
            "grocer");

        result.Tasks.ShouldContain(t => t.Title == "Buy groceries");
    }

    // --- Pagination (AC3 / AC5) ---

    [Fact]
    public async Task GetResultsAsync_ReturnsOnlyLimitResults_WhenMoreTasksExist()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        for (var i = 1; i <= 25; i++)
        {
            await AddTaskToDatabaseAsync(
                otherUser.Id,
                title: $"Groceries run {i}",
                description: "Remember to buy groceries");
        }

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 1, limit: 20);

        result.Tasks.Count.ShouldBe(20);
    }

    [Fact]
    public async Task GetResultsAsync_SecondPage_ContainsNoDuplicateTasksFromFirstPage()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        for (var i = 1; i <= 25; i++)
        {
            await AddTaskToDatabaseAsync(
                otherUser.Id,
                title: $"Groceries run {i}",
                description: "Remember to buy groceries");
        }

        var firstPage = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 1, limit: 20);
        var secondPage = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 2, limit: 20);

        var firstPageIds = firstPage.Tasks.Select(t => t.Id).ToHashSet();
        var secondPageIds = secondPage.Tasks.Select(t => t.Id).ToHashSet();

        secondPageIds.ShouldNotBeEmpty();
        secondPageIds.Intersect(firstPageIds).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetResultsAsync_SecondPage_ReturnsRemainingTasks()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        // 25 matches, page size 20 -> page 2 should have exactly 5.
        for (var i = 1; i <= 25; i++)
        {
            await AddTaskToDatabaseAsync(
                otherUser.Id,
                title: $"Groceries run {i}",
                description: "Remember to buy groceries");
        }

        var secondPage = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 2, limit: 20);

        secondPage.Tasks.Count.ShouldBe(5);
    }

    [Fact]
    public async Task GetResultsAsync_PreservesTaskOrdering_AcrossPageBoundary()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        var tasks = new List<TaskItem>();
        for (var i = 1; i <= 25; i++)
        {
            var task = await AddTaskToDatabaseAsync(
                otherUser.Id,
                title: $"Groceries run {i}",
                description: "Remember to buy groceries");
            tasks.Add(task);
        }

        // Give the last two tasks distinct, non-tied interaction counts so
        // ordering is fully deterministic across the page boundary.
        await AddComment(tasks[^1].Id, user.Id, "comment 1");
        await AddComment(tasks[^1].Id, user.Id, "comment 2");
        await AddComment(tasks[^2].Id, user.Id, "comment 1");

        var fullResult = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 1, limit: 100);
        var firstPage = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 1, limit: 20);
        var secondPage = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 2, limit: 20);

        var combined = firstPage.Tasks.Concat(secondPage.Tasks).Select(t => t.Id).ToList();
        var expected = fullResult.Tasks.Select(t => t.Id).ToList();

        combined.ShouldBe(expected);
    }

    [Fact]
    public async Task GetResultsAsync_PageBeyondLastTaskPage_ReturnsEmptyTasks()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Buy groceries");

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 5, limit: 20);

        result.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsOnlyLimitResults_WhenMoreUsersExist()
    {
        var user = await AddUserToDatabaseAsync();

        for (var i = 1; i <= 25; i++)
        {
            await AddUserToDatabaseAsync($"jane{i}@example.com", "Jane");
        }

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "Jane", page: 1, limit: 20);

        result.Users.Count.ShouldBe(20);
    }

    [Fact]
    public async Task GetResultsAsync_SecondPage_ContainsNoDuplicateUsersFromFirstPage()
    {
        var user = await AddUserToDatabaseAsync();

        for (var i = 1; i <= 25; i++)
        {
            await AddUserToDatabaseAsync($"jane{i}@example.com", "Jane");
        }

        var firstPage = await ServiceUnderTest.GetResultsAsync(user.Id, "Jane", page: 1, limit: 20);
        var secondPage = await ServiceUnderTest.GetResultsAsync(user.Id, "Jane", page: 2, limit: 20);

        var firstPageIds = firstPage.Users.Select(u => u.Id).ToHashSet();
        var secondPageIds = secondPage.Users.Select(u => u.Id).ToHashSet();

        secondPageIds.ShouldNotBeEmpty();
        secondPageIds.Intersect(firstPageIds).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetResultsAsync_TotalPages_ReflectsWhicheverResultTypeHasMorePages()
    {
 
        var user = await AddUserToDatabaseAsync();

        for (var i = 1; i <= 25; i++)
        {
            await AddUserToDatabaseAsync($"jane{i}@example.com", "Jane");
        }

        await AddTaskToDatabaseAsync(
            (await AddUserToDatabaseAsync("owner@example.com")).Id,
            title: "Jane's task");

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "Jane", page: 1, limit: 20);

        result.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task GetResultsAsync_SecondPage_StillReturnsUsers_WhenTasksAlreadyExhausted()
    {

        var user = await AddUserToDatabaseAsync();
        var taskOwner = await AddUserToDatabaseAsync("owner@example.com");

        await AddTaskToDatabaseAsync(taskOwner.Id, title: "Jane's task");

        for (var i = 1; i <= 25; i++)
        {
            await AddUserToDatabaseAsync($"jane{i}@example.com", "Jane");
        }

        var secondPage = await ServiceUnderTest.GetResultsAsync(user.Id, "Jane", page: 2, limit: 20);

        secondPage.Tasks.ShouldBeEmpty();
        secondPage.Users.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetResultsAsync_ReportsTotalPagesOfOne_WhenResultsFitOnOnePage()
    {
        var user = await AddUserToDatabaseAsync();
        var otherUser = await AddUserToDatabaseAsync("other@example.com");

        await AddTaskToDatabaseAsync(
            otherUser.Id,
            title: "Buy groceries");

        var result = await ServiceUnderTest.GetResultsAsync(user.Id, "groceries", page: 1, limit: 20);

        result.TotalPages.ShouldBe(1);
    }
}