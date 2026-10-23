using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grinder.Api.Tests.Integration.Controllers;

public class TasksControllerTest : BaseIntegrationTestFixture
{
    public TasksControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

    private User _user = null!;

    private async Task RegisterAndLogin()
    {
        var existingUser = new User
        {
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, "Password123!");

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var loginUserRequest = new LoginRequest("test@test.com", "Password123!");

        var loginResponse = await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }

    // Runs before every test so we don't need to call RegisterAndLogin every test
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    private async Task<User> AddVerifiedUserToDatabaseAsync(
        string email,
        string displayName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            IsVerified = true,
            CreatedAt = TestNow.UtcDateTime
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task AddMutualFollowAsync(User firstUser, User secondUser)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.AddRange(
            new Follow { FollowerId = firstUser.Id, FollowedId = secondUser.Id },
            new Follow { FollowerId = secondUser.Id, FollowedId = firstUser.Id });
        await context.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<TaskItem>> AddSampleTasksToDatabase(User user, int count)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        var tasks = new List<TaskItem>();

        for (var i = 0; i < count; i++)
        {
            tasks.Add(new TaskItem
            {
                Title = $"Test Task {i}",
                Description = $"Description {i}",
                DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(i + 1)),
                DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(i + 1)),
                ItemStatus = TaskItemStatus.Todo,
                CreatedAt = TestNow,
                UserId = user.Id
            });
        }

        dbContext.TaskItems.AddRange(tasks);
        await dbContext.SaveChangesAsync();

        return (tasks);
    }

    private async Task<IReadOnlyList<Category>> AddSampleCategoriesToDatabase(int count)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        var categories = new List<Category>();
        for (var i = 0; i < count; i++)
        {
            categories.Add(new Category
            {
                Name = $"Category {i}"
            });
        }

        dbContext.Category.AddRange(categories);
        await dbContext.SaveChangesAsync();

        return categories;
    }

    [Fact]
    public async Task GetTasks_NoTasksExist_EmptyListReturned()
    {
        var response = await HttpClient.GetAsync("/api/Tasks/user");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tasksReturned = await response.Content.ReadFromJsonAsync<IEnumerable<TaskItem>>();
        tasksReturned.ShouldNotBeNull();
        tasksReturned.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task GetTasks_TasksExist_CorrectNumberReturned(int count)
    {
        var tasksInDatabase = await AddSampleTasksToDatabase(_user, count);

        var response = await HttpClient.GetAsync("/api/Tasks/user");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tasksReturned = await response.Content.ReadFromJsonAsync<ICollection<TaskItem>>();
        tasksReturned.ShouldNotBeNull();
        tasksReturned.Count.ShouldBe(count);

        tasksReturned.Select(t => t.Title)
            .ShouldBeEquivalentTo(tasksInDatabase.Select(t => t.Title));
    }

    [Fact]
    public async Task GetTask_TaskExists_ReturnsOkAndTask()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 3);
        var expected = tasks[1];

        var response = await HttpClient.GetAsync($"/api/Tasks/{expected.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskResponse>();
        returned.ShouldNotBeNull();
        returned!.Id.ShouldBe(expected.Id);
        returned.Title.ShouldBe(expected.Title);
    }

    [Fact]
    public async Task GetTask_TaskDoesNotExist_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync("/api/Tasks/999999");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTask_ValidRequest_ReturnsCreated()
    {
        var payload = new CreateTaskRequest
        {
            Title = "Created Task",
            Description = "Created Desc",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(2)),
            ItemStatus = TaskItemStatus.Todo
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Tasks", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>();
        created.ShouldNotBeNull();
        created!.Title.ShouldBe("Created Task");
        created.Description.ShouldBe("Created Desc");
        created.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)));
        created.DueTime.ShouldBe(TimeOnly.FromDateTime(TestNow.DateTime.AddHours(2)));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var taskInDb = await context.TaskItems.FirstAsync(t => t.Id == created.Id);
        taskInDb.UserId.ShouldBe(_user.Id);
    }

    [Fact]
    public async Task CreateTask_DueDateInPast_ReturnsBadRequest()
    {
        var payload = new CreateTaskRequest
        {
            Title = "Created Task",
            Description = "Created Desc",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(-2)),
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(-2)),
            ItemStatus = TaskItemStatus.Todo
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Tasks", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await using var db = await DbContextFactory.CreateDbContextAsync();
        db.TaskItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task PatchTask_ValidRequest_ReturnsOkAndUpdatedTask()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Title = "New Title",
            Description = "New Desc",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)).ToString(),
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(5)).ToString(),
            ItemStatus = TaskItemStatus.Todo
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTask_TaskDoesNotExist_ReturnsNotFound()
    {
        var payload = new PatchTaskRequest
        {
            Title = "New Title",
            Description = "New Desc",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)).ToString(),
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(5)).ToString(),
            ItemStatus = TaskItemStatus.Todo
        };
        var response = await HttpClient.PatchAsJsonAsync("/api/Tasks/999999", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }


    [Fact]
    public async Task PatchTask_Unauthenticated_ReturnsUnauthorized()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Title = "Hacker has entered the chat"
        };
        var response = await UnauthenticatedClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchTask_DueDateInPast_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(-5)).ToString(),
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(-5)).ToString(),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_ClearDescription_ReturnsOkWithEmptyDescription()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Description = "",
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskItem>();
        returned.ShouldNotBeNull();
        returned.Description.ShouldBe("");
    }

    [Fact]
    public async Task PatchTask_TitleTooShort_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Title = "Ne",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)).ToString(),
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(5)).ToString(),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_TitleExactlyThreeChars_ReturnsOk()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Title = "ABC",
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTask_TitleExactly128Chars_ReturnsOk()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Title = new string('A', 128),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTask_TitleExactly129Characters_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Title = new string('A', 129),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_DescriptionExactly2048Characters_ReturnOk()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Description = new string('A', 2048),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTask_DescriptionExactly2049Characters_ReturnBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Description = new string('A', 2049),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_OnlyStatusUpdated_ReturnsOkAndUpdatesStatus()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            ItemStatus = TaskItemStatus.InProgress
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskItem>();
        returned.ShouldNotBeNull();
        returned.ItemStatus.ShouldBe(TaskItemStatus.InProgress);
    }

    [Fact]
    public async Task PatchTask_DueDateIsPatched_ReturnsOkAndUpdatesDueDate()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)).ToString(),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskItem>();
        returned.ShouldNotBeNull();
        returned.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)));
    }

    [Fact]
    public async Task ReorderTasks_ReturnsOk()
    {
        var items = new List<ReorderTasksRequest>
    {
        new(1, null, 0),
        new(2, null, 0)
    };
        var response = await HttpClient.PatchAsJsonAsync($"/api/tasks/reorder", items);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTask_DueTimeIsPatched_ReturnsOkAndUpdatesDueTime()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)).ToString(), // due time cannot be updated without due date
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(5)).ToString(),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskItem>();
        returned.ShouldNotBeNull();
        returned.DueTime.ShouldBe(new TimeOnly(TestNow.DateTime.AddHours(5).Hour, TestNow.DateTime.AddHours(5).Minute));
    }

    [Fact]
    public async Task PatchTask_ClearDueDate_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            DueDate = ""
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_DueTimeProvidedWithoutDueDate_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            DueDate = "",
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(5)).ToString(),
        };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetValidParentTasks_ReturnsOnlyDepth0And1Tasks()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1); // depth 0 by default

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            var child = new TaskItem
            {
                Title = "Child Task",
                ParentTaskId = tasks[0].Id,
                Depth = 1,
                ItemStatus = TaskItemStatus.Todo,
                CreatedAt = TestNow,
                UserId = _user.Id
            };
            context.TaskItems.Add(child);
            await context.SaveChangesAsync();

            var grandchild = new TaskItem
            {
                Title = "Grandchild Task",
                ParentTaskId = child.Id,
                Depth = 2,
                ItemStatus = TaskItemStatus.Todo,
                CreatedAt = TestNow,
                UserId = _user.Id
            };
            context.TaskItems.Add(grandchild);
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/tasks/parents");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var parents = await response.Content.ReadFromJsonAsync<List<ValidParentTask>>();
        parents.ShouldNotBeNull();
        parents.Select(p => p.Title).ShouldContain("Test Task 0");
        parents.Select(p => p.Title).ShouldContain("Child Task");
        parents.Select(p => p.Title).ShouldNotContain("Grandchild Task");
    }

    [Fact]
    public async Task CreateTask_WithValidParent_CreatesNestedTask()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new CreateTaskRequest
        {
            Title = "Child Task",
            ParentTaskId = tasks[0].Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Tasks", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<TaskResponse>();
        created!.ParentTaskId.ShouldBe(tasks[0].Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var taskInDb = await context.TaskItems.FirstAsync(t => t.Id == created.Id);
        taskInDb.Depth.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTask_ParentDoesNotExist_ReturnsBadRequest()
    {
        var payload = new CreateTaskRequest
        {
            Title = "Orphan Task",
            ParentTaskId = 999999,
            ItemStatus = TaskItemStatus.Todo
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Tasks", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTask_ParentAtMaxDepth_ReturnsBadRequest()
    {
        TaskItem grandchild;
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            var root = new TaskItem { Title = "Root", Depth = 0, ItemStatus = TaskItemStatus.Todo, CreatedAt = TestNow, UserId = _user.Id };
            context.TaskItems.Add(root);
            await context.SaveChangesAsync();

            var child = new TaskItem { Title = "Child", ParentTaskId = root.Id, Depth = 1, ItemStatus = TaskItemStatus.Todo, CreatedAt = TestNow, UserId = _user.Id };
            context.TaskItems.Add(child);
            await context.SaveChangesAsync();

            grandchild = new TaskItem { Title = "Grandchild", ParentTaskId = child.Id, Depth = 2, ItemStatus = TaskItemStatus.Todo, CreatedAt = TestNow, UserId = _user.Id };
            context.TaskItems.Add(grandchild);
            await context.SaveChangesAsync();
        }

        var payload = new CreateTaskRequest
        {
            Title = "Too Deep",
            ParentTaskId = grandchild.Id,
            ItemStatus = TaskItemStatus.Todo
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Tasks", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_ClearParent_SetsDepthToZero()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        TaskItem child;
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            child = new TaskItem { Title = "Child", ParentTaskId = tasks[0].Id, Depth = 1, ItemStatus = TaskItemStatus.Todo, CreatedAt = TestNow, UserId = _user.Id };
            context.TaskItems.Add(child);
            await context.SaveChangesAsync();
        }

        var payload = new PatchTaskRequest { ParentTaskId = -1 };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{child.Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskResponse>();
        returned!.ParentTaskId.ShouldBeNull();

        await using var context2 = await DbContextFactory.CreateDbContextAsync();
        var taskInDb = await context2.TaskItems.FirstAsync(t => t.Id == child.Id);
        taskInDb.Depth.ShouldBe(0);
    }

    [Fact]
    public async Task PatchTask_SetParentToItself_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);

        var payload = new PatchTaskRequest { ParentTaskId = tasks[0].Id };
        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_StartDateAndTime_ReturnsOkAndUpdatesFields()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            StartDate = DateOnly.FromDateTime(TestNow.DateTime).ToString(),
            StartTime = new TimeOnly(9, 0).ToString()
        };

        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskItem>();
        returned!.StartDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime));
        returned.StartTime.ShouldBe(new TimeOnly(9, 0));
    }

    [Fact]
    public async Task PatchTask_StartDateAfterDueDate_ReturnsBadRequest()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            StartDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(10)).ToString()
        };

        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchTask_CategoryIdsProvided_UpdatesCategoryLinksAndReturnsDto()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var categories = await AddSampleCategoriesToDatabase(2);

        var payload = new PatchTaskRequest
        {
            CategoryIds = categories.Select(c => c.Id).ToList()
        };

        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskResponse>();
        returned.ShouldNotBeNull();
        returned.Categories.Count.ShouldBe(2);
        returned.Categories.Select(c => c.Id)
            .OrderBy(id => id)
            .ShouldBe(categories.Select(c => c.Id).OrderBy(id => id));

        await using var db = await DbContextFactory.CreateDbContextAsync();
        var saved = await db.TaskItems
            .Include(t => t.Categories)
            .FirstAsync(t => t.Id == tasks[0].Id);

        saved.Categories.Count.ShouldBe(2);
        saved.Categories.Select(c => c.Id)
            .OrderBy(id => id)
            .ShouldBe(categories.Select(c => c.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task PatchTask_EmptyCategoryIds_ClearsAllCategoryLinks()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var categories = await AddSampleCategoriesToDatabase(2);

        await using (var seedContext = await DbContextFactory.CreateDbContextAsync())
        {
            var seededTask = await seedContext.TaskItems
                .Include(t => t.Categories)
                .FirstAsync(t => t.Id == tasks[0].Id);
            foreach (var category in categories)
            {
                seededTask.Categories.Add(category);
            }
            await seedContext.SaveChangesAsync();
        }

        var payload = new PatchTaskRequest
        {
            CategoryIds = new List<int>()
        };

        var response = await HttpClient.PatchAsJsonAsync($"/api/Tasks/{tasks[0].Id}", payload);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<TaskResponse>();
        returned.ShouldNotBeNull();
        returned.Categories.ShouldBeEmpty();

        await using var db = await DbContextFactory.CreateDbContextAsync();
        var saved = await db.TaskItems
            .Include(t => t.Categories)
            .FirstAsync(t => t.Id == tasks[0].Id);

        saved.Categories.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateTask_VisibilityProvided_ReturnsCreatedWithVisibility()
    {
        var payload = new CreateTaskRequest
        {
            Title = "Friends Task",
            ItemStatus = TaskItemStatus.Todo,
            DueDate = DateOnly.FromDateTime(TestNow.AddDays(2).DateTime),
            Visibility = TaskVisibility.FriendsOnly
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Tasks", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>();
        created.ShouldNotBeNull();
        created.Visibility.ShouldBe(TaskVisibility.FriendsOnly);
    }

    [Fact]
    public async Task PatchTask_VisibilityProvided_UpdatesVisibility()
    {
        var tasks = await AddSampleTasksToDatabase(_user, 1);
        var payload = new PatchTaskRequest
        {
            Visibility = TaskVisibility.Public
        };

        var response = await HttpClient.PatchAsJsonAsync(
            $"/api/Tasks/{tasks[0].Id}",
            payload);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TaskItem>();
        updated.ShouldNotBeNull();
        updated.Visibility.ShouldBe(TaskVisibility.Public);
    }

    private async Task<TaskItem> AddTaskWithVisibilityAsync(
    User user,
    TaskVisibility visibility,
    TaskItemStatus status = TaskItemStatus.Todo,
    int dueDaysFromNow = 2)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        var task = new TaskItem
        {
            Title = "Visibility Test Task",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(dueDaysFromNow)),
            DueTime = new TimeOnly(23, 59),
            ItemStatus = status,
            Visibility = visibility,
            CreatedAt = TestNow,
            UserId = user.Id
        };

        dbContext.TaskItems.Add(task);
        await dbContext.SaveChangesAsync();
        return task;
    }

    [Fact]
    public async Task GetStatusCount_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync($"/api/tasks/status-count/{_user.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatusCount_NoTasks_ReturnsAllZero()
    {
        var response = await HttpClient.GetAsync($"/api/tasks/status-count/{_user.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.Todo.ShouldBe(0);
        counts.InProgress.ShouldBe(0);
        counts.Done.ShouldBe(0);
        counts.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetStatusCount_OwnProfile_CountsPrivateTasks()
    {
        await AddTaskWithVisibilityAsync(_user, TaskVisibility.Private, TaskItemStatus.Todo);
        await AddTaskWithVisibilityAsync(_user, TaskVisibility.Private, TaskItemStatus.InProgress);

        var response = await HttpClient.GetAsync($"/api/tasks/status-count/{_user.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.Todo.ShouldBe(1);
        counts.InProgress.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatusCount_OtherUser_PublicTasksCounted()
    {
        var target = await AddVerifiedUserToDatabaseAsync("public-target@example.com", "Target");
        await AddTaskWithVisibilityAsync(target, TaskVisibility.Public, TaskItemStatus.Done);

        var response = await HttpClient.GetAsync($"/api/tasks/status-count/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.Done.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatusCount_OtherUser_PrivateTasksExcluded()
    {
        var target = await AddVerifiedUserToDatabaseAsync("private-target@example.com", "Target");
        await AddTaskWithVisibilityAsync(target, TaskVisibility.Private, TaskItemStatus.Todo);

        var response = await HttpClient.GetAsync($"/api/tasks/status-count/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.Todo.ShouldBe(0);
        counts.InProgress.ShouldBe(0);
        counts.Done.ShouldBe(0);
        counts.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetStatusCount_OtherUser_FriendsOnly_MutualFriend_Counted()
    {
        var target = await AddVerifiedUserToDatabaseAsync("friend-target@example.com", "Target");
        await AddMutualFollowAsync(_user, target);
        await AddTaskWithVisibilityAsync(target, TaskVisibility.FriendsOnly, TaskItemStatus.InProgress);

        var response = await HttpClient.GetAsync($"/api/tasks/status-count/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.InProgress.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatusCount_OtherUser_FriendsOnly_NotFriend_Excluded()
    {
        var target = await AddVerifiedUserToDatabaseAsync("nonfriend-target@example.com", "Target");
        await AddTaskWithVisibilityAsync(target, TaskVisibility.FriendsOnly, TaskItemStatus.InProgress);

        var response = await HttpClient.GetAsync($"/api/tasks/status-count/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.InProgress.ShouldBe(0);
    }

    [Fact]
    public async Task GetStatusCount_RequestedUserDoesNotExist_ReturnsOkWithZeroCounts()
    {
        var response = await HttpClient.GetAsync("/api/tasks/status-count/999999");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var counts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
        counts.ShouldNotBeNull();
        counts.Todo.ShouldBe(0);
        counts.Overdue.ShouldBe(0);
    }
}
