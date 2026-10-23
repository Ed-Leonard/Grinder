using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class TaskServiceTest : BaseIntegrationTestFixture
{
    private ITaskService ServiceUnderTest => ServiceProvider.GetRequiredService<ITaskService>();

    public TaskServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUserToDatabaseAsync(string email = "test@example.com")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var user = new User
        {
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
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

    private async Task<TaskItem> CreateTaskForTest(
        User user,
        string title = "Title",
        string? description = "Description",
        int dueDaysFromNow = 2,
        int? startDaysFromNow = null,
        TimeOnly? startTime = null,
        TimeOnly? dueTime = null,
        TaskItemStatus status = TaskItemStatus.Todo,
        TaskVisibility visibility = TaskVisibility.Private,
        int? parentTaskId = null)
    {
        return await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = title,
            Description = description,
            StartDate = startDaysFromNow.HasValue
                ? DateOnly.FromDateTime(TestNow.DateTime.AddDays(startDaysFromNow.Value))
                : null,
            StartTime = startTime,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(dueDaysFromNow)),
            DueTime = dueTime,
            ItemStatus = status,
            Visibility = visibility,
            ParentTaskId = parentTaskId
        });
    }

    private async Task<TaskAttachment> AddPendingImageAsync(int userId)
    {
        await using DatabaseContext context =
            await DbContextFactory.CreateDbContextAsync();

        TaskAttachment attachment = new()
        {
            UserId = userId,
            TaskItemId = null,
            StoredFileName = $"{Guid.NewGuid():N}.png",
            ContentType = "image/png",
            FileSize = 4,
            Url = "/uploads/taskAttachments/task-image.png",
            CreatedAt = TestNow.UtcDateTime
        };

        context.TaskAttachments.Add(attachment);
        await context.SaveChangesAsync();

        return attachment;
    }

    private async Task<TaskAttachment> AddPendingVideoAsync(int userId)
    {
        await using DatabaseContext context =
            await DbContextFactory.CreateDbContextAsync();

        TaskAttachment attachment = new()
        {
            UserId = userId,
            TaskItemId = null,
            StoredFileName = $"{Guid.NewGuid():N}.mp4",
            ContentType = "video/mp4",
            FileSize = 4,
            Url = "/uploads/taskAttachments/video.mp4",
            CreatedAt = TestNow.UtcDateTime
        };

        context.TaskAttachments.Add(attachment);
        await context.SaveChangesAsync();

        return attachment;
    }

    private async Task AssertTaskUnchangedAsync(TaskItem original)
    {
        await using var ctx = await DbContextFactory.CreateDbContextAsync();
        var actual = await ctx.TaskItems.AsNoTracking().FirstAsync(t => t.Id == original.Id);
        actual.Title.ShouldBe(original.Title);
        actual.Description.ShouldBe(original.Description);
        actual.DueDate.ShouldBe(original.DueDate);
        actual.DueTime.ShouldBe(original.DueTime);
        actual.ItemStatus.ShouldBe(original.ItemStatus);
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
            CreatedAt = TestNow.UtcDateTime
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateNewTaskItem_ValidDetails_TaskInDatabase()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var dueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2));
        var dueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(2));

        await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest { Title = "My Task", Description = "Task description", DueDate = dueDate, DueTime = dueTime, ItemStatus = TaskItemStatus.Todo });

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var taskInDb = context.TaskItems.ShouldHaveSingleItem();
        taskInDb.Id.ShouldBe(1);
        taskInDb.Title.ShouldBe("My Task");
        taskInDb.Description.ShouldBe("Task description");
        taskInDb.DueDate.ShouldBe(dueDate);
        taskInDb.DueTime.ShouldBe(dueTime);
        taskInDb.ItemStatus.ShouldBe(TaskItemStatus.Todo);
        taskInDb.CreatedAt.ShouldBe(TestNow);
    }

    [Fact]
    public async Task CreateNewTaskAsync_PendingImage_AttachesImageToTask()
    {

        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        TaskAttachment pendingImage = await AddPendingImageAsync(user.Id);
        CreateTaskRequest request = new()
        {
            Title = "Task with image",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            AttachmentIds = new List<int> { pendingImage.Id }
        };
        TaskItem createdTask = await ServiceUnderTest.CreateNewTaskAsync(
            user.Id,
            request
        );

        await using DatabaseContext context =
            await DbContextFactory.CreateDbContextAsync();

        TaskItem storedTask = await context.TaskItems
            .Include(task => task.TaskAttachments)
            .SingleAsync(task => task.Id == createdTask.Id);

        TaskAttachment storedImage =
            storedTask.TaskAttachments.ShouldHaveSingleItem();

        storedImage.Id.ShouldBe(pendingImage.Id);
        storedImage.TaskItemId.ShouldBe(createdTask.Id);
        storedImage.UserId.ShouldBe(user.Id);
        storedImage.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task PatchTaskAsync_PendingImage_AttachesImageToExistingTask()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        TaskItem existingTask = await CreateTaskForTest(user);
        TaskAttachment pendingImage = await AddPendingImageAsync(user.Id);

        PatchTaskRequest request = new()
        {
            AttachmentIds = new List<int> { pendingImage.Id }
        };

        TaskItem? updatedTask = await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            existingTask.Id,
            request
        );

        updatedTask.ShouldNotBeNull();

        await using DatabaseContext context =
            await DbContextFactory.CreateDbContextAsync();

        TaskItem storedTask = await context.TaskItems
            .Include(task => task.TaskAttachments)
            .SingleAsync(task => task.Id == existingTask.Id);

        TaskAttachment storedImage =
            storedTask.TaskAttachments.ShouldHaveSingleItem();

        storedImage.Id.ShouldBe(pendingImage.Id);
        storedImage.TaskItemId.ShouldBe(existingTask.Id);
        storedImage.UserId.ShouldBe(user.Id);
        storedImage.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task PatchTaskAsync_InvalidParentAfterAttachmentRequest_RollsBackChanges()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        TaskItem existingTask = await CreateTaskForTest(user, title: "Original Title");
        TaskAttachment pendingImage = await AddPendingImageAsync(user.Id);

        PatchTaskRequest request = new()
        {
            Title = "Updated Title",
            AttachmentIds = new List<int> { pendingImage.Id },
            ParentTaskId = 999999
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, existingTask.Id, request)
        );

        await using DatabaseContext context =
            await DbContextFactory.CreateDbContextAsync();

        TaskItem storedTask = await context.TaskItems
            .Include(task => task.TaskAttachments)
            .SingleAsync(task => task.Id == existingTask.Id);

        storedTask.Title.ShouldBe("Original Title");
        storedTask.TaskAttachments.ShouldBeEmpty();

        TaskAttachment storedImage = await context.TaskAttachments
            .SingleAsync(attachment => attachment.Id == pendingImage.Id);
        storedImage.TaskItemId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateNewTaskItem_NullDescription_Null_TaskStoredCorrectly()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await CreateTaskForTest(user, title: "Task Without Description", description: null, dueTime: null);

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var taskInDb = context.TaskItems.ShouldHaveSingleItem();
        taskInDb.Title.ShouldBe("Task Without Description");
        taskInDb.Description.ShouldBeNull();
        taskInDb.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)));
        taskInDb.DueTime.ShouldBe(new TimeOnly(23, 59));
        taskInDb.ItemStatus.ShouldBe(TaskItemStatus.Todo);
        taskInDb.CreatedAt.ShouldBe(TestNow);
    }

    [Fact]
    public async Task CreateNewTaskItem_DueDateInPast_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var pastDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(-5));

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.CreateNewTaskAsync(
                user.Id, new CreateTaskRequest
                {
                    Title = "Invalid Task",
                    Description = null,
                    DueDate = pastDate,
                    DueTime = null,
                    ItemStatus = TaskItemStatus.Todo
                })
        );

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateNewTaskItem_DueDateEqualNow_AllowsCreation()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await CreateTaskForTest(user, dueDaysFromNow: 0);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var task = context.TaskItems.ShouldHaveSingleItem();
        task.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime));
    }

    [Fact]
    public async Task GetAllTasks_NoTasksInDb_EmptyCollectionReturned()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var returnedTasks = await ServiceUnderTest.GetAllTasksAsync(user.Id, null);
        returnedTasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTasks_TasksExist_ReturnsAllTasks()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await CreateTaskForTest(user, title: "Task 1");
        await CreateTaskForTest(user, title: "Task 2");

        var all = (await ServiceUnderTest.GetAllTasksAsync(user.Id, null)).ToList();

        all.Count.ShouldBe(2);
        all.Select(t => t.Title).ShouldBe(new[] { "Task 1", "Task 2" });
    }

    [Fact]
    public async Task GetTaskByIdAsync_TaskExists_ReturnsTask()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var created = await CreateTaskForTest(user, dueDaysFromNow: 1);

        var fetched = await ServiceUnderTest.GetTaskByIdAsync(user.Id, created.Id);

        fetched.ShouldNotBeNull();
        fetched!.Id.ShouldBe(created.Id);
        fetched.Title.ShouldBe("Title");
        fetched.Description.ShouldBe("Description");
        fetched.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(1)));
        fetched.DueTime.ShouldBe(new TimeOnly(23, 59));
        fetched.ItemStatus.ShouldBe(TaskItemStatus.Todo);
    }

    [Fact]
    public async Task GetTaskByIdAsync_TaskDoesNotExist_ReturnsNull()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var fetched = await ServiceUnderTest.GetTaskByIdAsync(user.Id, 999999);
        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task PatchTaskAsync_AllFieldsChanged_TaskUpdated()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var newDueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(10));
        var newDueTime = new TimeOnly(TestNow.DateTime.AddHours(5).Hour, TestNow.DateTime.AddHours(5).Minute);

        var request = new PatchTaskRequest
        {
            Title = "New Title",
            Description = "New Desc",
            DueDate = newDueDate.ToString(),
            DueTime = newDueTime.ToString(),
            ItemStatus = TaskItemStatus.Done
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request);

        updated.ShouldNotBeNull();
        updated!.Title.ShouldBe("New Title");
        updated.Description.ShouldBe("New Desc");
        updated.DueDate.ShouldBe(newDueDate);
        updated.DueTime.ShouldBe(newDueTime);
        updated.ItemStatus.ShouldBe(TaskItemStatus.Done);
    }

    public static IEnumerable<object?[]> ValidTitleData =>
        new List<object?[]>
        {
            new object?[] { new string('A', 128), new string('A', 128) },
            new object?[] { new string('A', 3), new string('A', 3) },
            new object?[] { "   Valid Title   ", "Valid Title" }
        };

    [Theory]
    [MemberData(nameof(ValidTitleData))]
    public async Task PatchTaskAsync_ValidTitleProvided_UpdatesTitle(string? inputTitle, string expectedTitle)
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            Title = inputTitle
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request);

        updated.ShouldNotBeNull();
        updated.Title.ShouldBe(expectedTitle);
        updated.Description.ShouldBe("Description");
        updated.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)));
        updated.DueTime.ShouldBe(new TimeOnly(23, 59));
        updated.ItemStatus.ShouldBe(TaskItemStatus.Todo);
    }

    public static IEnumerable<object?[]> InvalidTitleData =>
        new List<object?[]>
        {
            new object?[] { "AA" },
            new object?[] { new string('A', 129) },
            new object?[] { "    " }
        };

    [Theory]
    [MemberData(nameof(InvalidTitleData))]
    public async Task PatchTaskAsync_InvalidTitleProvided_ThrowsException(string? inputTitle)
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            Title = inputTitle
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request)
        );

        await AssertTaskUnchangedAsync(created);
    }

    public static IEnumerable<object?[]> ValidDescriptionData =>
        new List<object?[]>
        {
            new object?[] { "A", "A" },
            new object?[] { new string('A', 2048), new string('A', 2048) },
            new object?[] { "", "" }
        };

    [Theory]
    [MemberData(nameof(ValidDescriptionData))]
    public async Task PatchTaskAsync_ValidDescriptionProvided_UpdatesDescription(string? inputDescription, string expectedDescription)
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            Description = inputDescription
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request);

        updated.ShouldNotBeNull();
        updated.Title.ShouldBe("Title");
        updated.Description.ShouldBe(expectedDescription);
        updated.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)));
        updated.DueTime.ShouldBe(new TimeOnly(23, 59));
        updated.ItemStatus.ShouldBe(TaskItemStatus.Todo);
    }

    [Fact]
    public async Task PatchTaskAsync_InvalidDescriptionProvided_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var longDescription = new string('A', 2049);
        var request = new PatchTaskRequest
        {
            Description = longDescription
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request)
        );

        await AssertTaskUnchangedAsync(created);
    }

    [Fact]
    public async Task PatchTaskAsync_ValidDueDateProvided_UpdatesDueDate()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(10)).ToString(),
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request);

        updated.ShouldNotBeNull();
        updated.Title.ShouldBe("Title");
        updated.Description.ShouldBe("Description");
        updated.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(10)));
        updated.DueTime.ShouldBe(new TimeOnly(23, 59));
        updated.ItemStatus.ShouldBe(TaskItemStatus.Todo);
    }

    [Fact]
    public async Task PatchTaskAsync_DueDateInPastProvided_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var pastDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(-1));

        var request = new PatchTaskRequest
        {
            DueDate = pastDate.ToString(),
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request)
        );

        await AssertTaskUnchangedAsync(created);
    }

    [Fact]
    public async Task PatchTaskAsync_ChangeDueTimeOfTaskWithExistingDate_UpdatesTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5)).ToString(), // must provide date when adding time
            DueTime = TimeOnly.FromDateTime(TestNow.DateTime.AddHours(5)).ToString()
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request);

        updated.ShouldNotBeNull();
        updated.DueDate.ShouldBe(DateOnly.Parse(request.DueDate));
        updated.DueTime.ShouldBe(TimeOnly.Parse(request.DueTime));
    }

    [Fact]
    public async Task PatchTaskAsync_InvalidDueDateFormatProvided_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            DueDate = "not-a-date"
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request)
        );

        await AssertTaskUnchangedAsync(created);
    }

    [Fact]
    public async Task PatchTaskAsync_InvalidDueTimeFormatProvided_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            DueTime = "not-a-time"
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request)
        );

        await AssertTaskUnchangedAsync(created);
    }

    [Fact]
    public async Task PatchTaskAsync_StatusProvided_UpdatesStatus()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            ItemStatus = TaskItemStatus.InProgress
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request);

        updated.ShouldNotBeNull();
        updated.Title.ShouldBe("Title");
        updated.Description.ShouldBe("Description");
        updated.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)));
        updated.ItemStatus.ShouldBe(TaskItemStatus.InProgress);
    }

    [Fact]
    public async Task CreateNewTaskAsync_DoneStatus_SetsCompletedAtToCreationTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var task = await CreateTaskForTest(user, status: TaskItemStatus.Done);

        task.CompletedAt.ShouldBe(TestNow);
    }

    [Fact]
    public async Task CreateNewTaskAsync_TodoStatus_LeavesCompletedAtNull()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var task = await CreateTaskForTest(user, status: TaskItemStatus.Todo);

        task.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task PatchTaskAsync_MarkDone_SetsCompletedAtToPatchTimeNotCreationTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);
        var createdAt = created.CreatedAt;

        FakeTimeProvider.Advance(TimeSpan.FromHours(1));

        var updated = await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            created.Id,
            new PatchTaskRequest { ItemStatus = TaskItemStatus.Done });

        updated.ShouldNotBeNull();
        updated!.CompletedAt.ShouldBe(FakeTimeProvider.GetUtcNow());
        updated.CompletedAt.ShouldNotBe(createdAt);
    }

    [Fact]
    public async Task PatchTaskAsync_ReopenDoneTask_ClearsCompletedAt()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user, status: TaskItemStatus.Done);
        created.CompletedAt.ShouldNotBeNull();

        var updated = await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            created.Id,
            new PatchTaskRequest { ItemStatus = TaskItemStatus.Todo });

        updated.ShouldNotBeNull();
        updated!.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task PatchTaskAsync_EditDoneTaskWithoutStatusChange_KeepsOriginalCompletedAt()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user, status: TaskItemStatus.Done);
        var originalCompletedAt = created.CompletedAt;

        FakeTimeProvider.Advance(TimeSpan.FromHours(1));

        var updated = await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            created.Id,
            new PatchTaskRequest { Title = "Renamed done task" });

        updated.ShouldNotBeNull();
        updated!.CompletedAt.ShouldBe(originalCompletedAt);
        updated.UpdatedAt.ShouldBe(FakeTimeProvider.GetUtcNow());
    }

    [Fact]
    public async Task ReorderTasks_ShouldUpdateOrders()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, title: "Task 1");
        var task2 = await CreateTaskForTest(user, title: "Task 2");
        var task3 = await CreateTaskForTest(user, title: "Task 3");

        await ServiceUnderTest.ReorderTasks(new List<ReorderTasksRequest>
        {
            new(task1.Id, null, 3),
            new(task2.Id, null, 1)
        }, user.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var tasks = await context.TaskItems
            .OrderBy(t => t.Id)
            .ToListAsync();

        tasks.Count.ShouldBe(3);
        tasks[0].Order.ShouldBe(3);
        tasks[1].Order.ShouldBe(1);
        tasks[2].Order.ShouldBe(2);
    }

    [Fact]
    public async Task CreateNewTaskAsync_SingleVideoAttachment_SetsIsClipTrue()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var video = await AddPendingVideoAsync(user.Id);
        CreateTaskRequest request = new()
        {
            Title = "Task with image",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            AttachmentIds = new List<int> { video.Id }
        };

        var task = await ServiceUnderTest.CreateNewTaskAsync(
            user.Id,
            request
        );

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var storedTask = await context.TaskItems
            .SingleAsync(t => t.Id == task.Id);

        storedTask.IsClip.ShouldBeTrue();
    }

    [Fact]
    public async Task PatchTaskAsync_AddSingleVideoAttachment_SetsIsClipTrue()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var task = await CreateTaskForTest(user);

        var video = await AddPendingVideoAsync(user.Id);

        await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            task.Id,
            new PatchTaskRequest
            {
                AttachmentIds = new List<int> { video.Id }
            });

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var updated = await context.TaskItems
            .SingleAsync(t => t.Id == task.Id);

        updated.IsClip.ShouldBeTrue();
    }

    [Fact]
    public async Task PatchTaskAsync_RemoveExistingVideoAddImage_SetsIsClipFalse()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var video = await AddPendingVideoAsync(user.Id);

        CreateTaskRequest request = new()
        {
            Title = "Task with image",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            AttachmentIds = new List<int> { video.Id }
        };
        var task = await ServiceUnderTest.CreateNewTaskAsync(
            user.Id,
            request);

        task.IsClip.ShouldBeTrue();

        var image = await AddPendingImageAsync(user.Id);

        await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            task.Id,
            new PatchTaskRequest
            {
                AttachmentIds = new List<int> { image.Id }
            });

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var updated = await context.TaskItems
            .Include(t => t.TaskAttachments)
            .SingleAsync(t => t.Id == task.Id);
        updated.IsClip.ShouldBeFalse();
    }


    [Fact]
    public async Task CreateNewTaskAsync_TwoVideoAttachments_SetsIsClipFalse()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var video = await AddPendingVideoAsync(user.Id);
        var video2 = await AddPendingVideoAsync(user.Id);

        CreateTaskRequest request = new()
        {
            Title = "Task with image",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            AttachmentIds = new List<int> { video.Id, video2.Id }
        };

        var task = await ServiceUnderTest.CreateNewTaskAsync(
            user.Id,
            request);

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var storedTask = await context.TaskItems
            .SingleAsync(t => t.Id == task.Id);

        storedTask.IsClip.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateNewTaskAsync_SingleVideoAttachment_SetsClipCreatedAtToCreationTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var video = await AddPendingVideoAsync(user.Id);
        CreateTaskRequest request = new()
        {
            Title = "Task with video",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            AttachmentIds = new List<int> { video.Id }
        };

        var task = await ServiceUnderTest.CreateNewTaskAsync(user.Id, request);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var storedTask = await context.TaskItems.SingleAsync(t => t.Id == task.Id);

        storedTask.ClipCreatedAt.ShouldBe(TestNow);
    }

    [Fact]
    public async Task PatchTaskAsync_AddSingleVideoAttachment_SetsClipCreatedAtToPatchTimeNotTaskCreationTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var task = await CreateTaskForTest(user);
        var taskCreatedAt = task.CreatedAt;

        FakeTimeProvider.Advance(TimeSpan.FromHours(1));
        var video = await AddPendingVideoAsync(user.Id);

        await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            task.Id,
            new PatchTaskRequest
            {
                AttachmentIds = new List<int> { video.Id }
            });

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var updated = await context.TaskItems.SingleAsync(t => t.Id == task.Id);

        updated.CreatedAt.ShouldBe(taskCreatedAt);
        updated.ClipCreatedAt.ShouldBe(FakeTimeProvider.GetUtcNow());
        updated.ClipCreatedAt.ShouldNotBe(updated.CreatedAt);
    }

    [Fact]
    public async Task PatchTaskAsync_RemoveExistingVideoAddImage_ClearsClipCreatedAt()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var video = await AddPendingVideoAsync(user.Id);

        CreateTaskRequest request = new()
        {
            Title = "Task with image",
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo,
            AttachmentIds = new List<int> { video.Id }
        };
        var task = await ServiceUnderTest.CreateNewTaskAsync(user.Id, request);
        task.ClipCreatedAt.ShouldNotBeNull();

        var image = await AddPendingImageAsync(user.Id);

        await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            task.Id,
            new PatchTaskRequest
            {
                AttachmentIds = new List<int> { image.Id }
            });

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var updated = await context.TaskItems
            .Include(t => t.TaskAttachments)
            .SingleAsync(t => t.Id == task.Id);

        updated.ClipCreatedAt.ShouldBeNull();
    }
    // ==================== Start Date/Time Tests ====================

    [Fact]
    public async Task CreateNewTaskAsync_ValidStartDateAndTime_TaskPersisted()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, startDaysFromNow: 1, startTime: new TimeOnly(10, 30), dueDaysFromNow: 3);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var taskInDb = context.TaskItems.ShouldHaveSingleItem();
        taskInDb.DueDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(3)));
        taskInDb.StartDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(1)));
        taskInDb.StartTime.ShouldBe(new TimeOnly(10, 30));
    }

    [Fact]
    public async Task CreateNewTaskAsync_StartDateAfterDueDate_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var startDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5));
        var dueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2));

        var task1 = await Should.ThrowAsync<ArgumentException>(() => CreateTaskForTest(user, startDaysFromNow: 5, dueDaysFromNow: 2));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateNewTaskAsync_StartTimeWithoutStartDate_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await Should.ThrowAsync<ArgumentException>(() => CreateTaskForTest(user, startTime: new TimeOnly(10, 0), dueDaysFromNow: 2));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateNewTaskAsync_StartDateWithoutStartTime_DefaultsToMidnight()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, startDaysFromNow: 1, dueDaysFromNow: 3);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var taskInDb = context.TaskItems.ShouldHaveSingleItem();
        taskInDb.StartDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(1)));
        taskInDb.StartTime.ShouldBe(new TimeOnly(0, 0));
    }

    [Fact]
    public async Task CreateNewTaskAsync_EqualStartAndDueDateTime_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await Should.ThrowAsync<ArgumentException>(() => CreateTaskForTest(user, startDaysFromNow: 1, dueDaysFromNow: 1, startTime: new TimeOnly(10, 0), dueTime: new TimeOnly(10, 0)));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task PatchTaskAsync_SetStartDateAndTime_UpdatesTask()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user);

        var startDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(1));
        var startTime = new TimeOnly(9, 0);

        var request = new PatchTaskRequest
        {
            StartDate = startDate.ToString(),
            StartTime = startTime.ToString()
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, task1.Id, request);

        updated.ShouldNotBeNull();
        updated!.StartDate.ShouldBe(startDate);
        updated.StartTime.ShouldBe(startTime);
    }

    [Fact]
    public async Task PatchTaskAsync_StartDateAfterDueDate_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, dueDaysFromNow: 2);

        var startDateAfterDue = DateOnly.FromDateTime(TestNow.DateTime.AddDays(5));

        var request = new PatchTaskRequest
        {
            StartDate = startDateAfterDue.ToString()
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, task1.Id, request)
        );

        await AssertTaskUnchangedAsync(task1);
    }

    [Fact]
    public async Task PatchTaskAsync_ClearStartDate_ClearsBothDateAndTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, startTime: new TimeOnly(10, 0), startDaysFromNow: 1);

        var request = new PatchTaskRequest
        {
            StartDate = ""
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, task1.Id, request);

        updated.ShouldNotBeNull();
        updated!.StartDate.ShouldBeNull();
        updated.StartTime.ShouldBeNull();
    }

    [Fact]
    public async Task PatchTaskAsync_StartTimeOnlyUpdate_UpdatesTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, startTime: new TimeOnly(10, 0), startDaysFromNow: 1);

        var newStartTime = new TimeOnly(11, 30);

        var request = new PatchTaskRequest
        {
            StartTime = newStartTime.ToString()
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, task1.Id, request);

        updated.ShouldNotBeNull();
        updated!.StartDate.ShouldBe(DateOnly.FromDateTime(TestNow.DateTime.AddDays(1)));
        updated.StartTime.ShouldBe(newStartTime);
    }

    [Fact]
    public async Task PatchTaskAsync_StartDateWithoutStartTime_DefaultsToMidnight()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user);

        var startDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(1));

        var request = new PatchTaskRequest
        {
            StartDate = startDate.ToString()
        };

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, task1.Id, request);

        updated.ShouldNotBeNull();
        updated!.StartDate.ShouldBe(startDate);
        updated.StartTime.ShouldBe(new TimeOnly(0, 0));
    }

    [Fact]
    public async Task PatchTaskAsync_EqualStartAndDueDateTime_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, dueDaysFromNow: 2, dueTime: new TimeOnly(14, 0));


        var sameDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2));
        var sameTime = new TimeOnly(14, 0);

        var request = new PatchTaskRequest
        {
            StartDate = sameDate.ToString(),
            StartTime = sameTime.ToString()
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, task1.Id, request)
        );

        await AssertTaskUnchangedAsync(task1);
    }

    [Fact]
    public async Task ReorderTasksAsync_CreateCycleOfParents_ThrowsArgumentException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task1 = await CreateTaskForTest(user, dueDaysFromNow: 2, dueTime: new TimeOnly(14, 0));
        var task2 = await CreateTaskForTest(user, dueDaysFromNow: 2, dueTime: new TimeOnly(14, 0));


        // create cycle
        var request = new List<ReorderTasksRequest>
        {
            new(task1.Id, task2.Id, 0),
            new(task2.Id, task1.Id, 1)
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.ReorderTasks(request, user.Id)
        );

        await AssertTaskUnchangedAsync(task1);
        await AssertTaskUnchangedAsync(task2);
    }

    [Theory]
    [InlineData(TaskVisibility.Private)]
    [InlineData(TaskVisibility.FriendsOnly)]
    [InlineData(TaskVisibility.Public)]
    public async Task CreateNewTaskAsync_VisibilityProvided_PersistsVisibility(
        TaskVisibility visibility)
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var created = await CreateTaskForTest(user, visibility: visibility);

        created.Visibility.ShouldBe(visibility);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskItems.FindAsync(created.Id))!.Visibility.ShouldBe(visibility);
    }

    [Fact]
    public async Task PatchTaskAsync_VisibilityProvided_UpdatesVisibility()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var updated = await ServiceUnderTest.PatchTaskAsync(
            user.Id,
            created.Id,
            new PatchTaskRequest { Visibility = TaskVisibility.FriendsOnly });

        updated.ShouldNotBeNull();
        updated.Visibility.ShouldBe(TaskVisibility.FriendsOnly);
    }

    [Fact]
    public async Task DeleteTaskAsync_OwnedTaskWithChildren_DeletesTaskTree()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var parent = await CreateTaskForTest(user);
        var child = await CreateTaskForTest(user, parentTaskId: parent.Id);
        await CreateTaskForTest(user, parentTaskId: child.Id);

        var deleted = await ServiceUnderTest.DeleteTaskAsync(user.Id, parent.Id);

        deleted.ShouldBeTrue();
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteTaskAsync_TaskOwnedByAnotherUser_ReturnsFalse()
    {
        var owner = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var otherUser = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await CreateTaskForTest(owner);

        var deleted = await ServiceUnderTest.DeleteTaskAsync(otherUser.Id, task.Id);

        deleted.ShouldBeFalse();
        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskItems.FindAsync(task.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateNewTaskAsync_ValidParent_SetsDepthCorrectly()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var root = await CreateTaskForTest(user, title: "Root");

        var child = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Child",
            ParentTaskId = root.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });

        child.Depth.ShouldBe(1);
        child.ParentTaskId.ShouldBe(root.Id);
    }

    [Fact]
    public async Task CreateNewTaskAsync_ParentNotFound_ThrowsArgumentException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
            {
                Title = "Orphan",
                ParentTaskId = 999999,
                DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
                ItemStatus = TaskItemStatus.Todo
            })
        );
    }

    [Fact]
    public async Task CreateNewTaskAsync_ParentAtMaxDepth_ThrowsInvalidOperationException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var root = await CreateTaskForTest(user, title: "Root");
        var child = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Child",
            ParentTaskId = root.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });
        var grandchild = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Grandchild",
            ParentTaskId = child.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
            {
                Title = "Too Deep",
                ParentTaskId = grandchild.Id,
                DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
                ItemStatus = TaskItemStatus.Todo
            })
        );
    }

    [Fact]
    public async Task PatchTaskAsync_SetParentToNegativeOne_ClearsParentAndDepth()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var root = await CreateTaskForTest(user, title: "Root");
        var child = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Child",
            ParentTaskId = root.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });

        var updated = await ServiceUnderTest.PatchTaskAsync(user.Id, child.Id, new PatchTaskRequest { ParentTaskId = -1 });

        updated.ShouldNotBeNull();
        updated!.ParentTaskId.ShouldBeNull();
        updated.Depth.ShouldBe(0);
    }

    [Fact]
    public async Task PatchTaskAsync_ParentWouldCreateCycle_ThrowsArgumentException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var root = await CreateTaskForTest(user, title: "Root");
        var child = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Child",
            ParentTaskId = root.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, root.Id, new PatchTaskRequest { ParentTaskId = child.Id })
        );

        await AssertTaskUnchangedAsync(root);
    }

    [Fact]
    public async Task GetAllValidParentTasksAsync_ExcludesDepthTwoTasks()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var root = await CreateTaskForTest(user, title: "Root");
        var child = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Child",
            ParentTaskId = root.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });
        await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Grandchild",
            ParentTaskId = child.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });

        var validParents = (await ServiceUnderTest.GetAllValidParentTasksAsync(user.Id)).ToList();

        validParents.Select(p => p.Title).ShouldContain("Root");
        validParents.Select(p => p.Title).ShouldContain("Child");
        validParents.Select(p => p.Title).ShouldNotContain("Grandchild");
    }

    [Fact]
    public async Task PatchTaskAsync_CategoryIdNotFound_ThrowsException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var created = await CreateTaskForTest(user);

        var request = new PatchTaskRequest
        {
            CategoryIds = new List<int> { 999999 } // does not exist
        };

        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.PatchTaskAsync(user.Id, created.Id, request)
        );

        await AssertTaskUnchangedAsync(created);
    }

    [Fact]
    public async Task GetAllTasksAsync_OwnProfileRequestedUserIdNull_ReturnsAllVisibilities()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(user, title: "Private Task", visibility: TaskVisibility.Private);
        await CreateTaskForTest(user, title: "Friends Task", visibility: TaskVisibility.FriendsOnly);
        await CreateTaskForTest(user, title: "Public Task", visibility: TaskVisibility.Public);

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(user.Id, null)).ToList();

        tasks.Count.ShouldBe(3);
        tasks.Select(t => t.Title).ShouldContain("Private Task");
        tasks.Select(t => t.Title).ShouldContain("Friends Task");
        tasks.Select(t => t.Title).ShouldContain("Public Task");
    }

    [Fact]
    public async Task GetAllTasksAsync_RequestedUserIdEqualsUserId_ReturnsAllVisibilities()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(user, title: "Private Task", visibility: TaskVisibility.Private);

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(user.Id, user.Id)).ToList();

        tasks.Select(t => t.Title).ShouldContain("Private Task");
    }

    [Fact]
    public async Task GetAllTasksAsync_ViewingFriendsProfile_ReturnsPublicAndFriendsOnly_ExcludesPrivate()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var owner = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await AddMutualFollowAsync(viewer, owner);

        await CreateTaskForTest(owner, title: "Private Task", visibility: TaskVisibility.Private);
        await CreateTaskForTest(owner, title: "Friends Task", visibility: TaskVisibility.FriendsOnly);
        await CreateTaskForTest(owner, title: "Public Task", visibility: TaskVisibility.Public);

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(viewer.Id, owner.Id)).ToList();

        tasks.Count.ShouldBe(2);
        tasks.Select(t => t.Title).ShouldContain("Friends Task");
        tasks.Select(t => t.Title).ShouldContain("Public Task");
        tasks.Select(t => t.Title).ShouldNotContain("Private Task");
    }

    [Fact]
    public async Task GetAllTasksAsync_ViewingNonFriendsProfile_ReturnsOnlyPublic()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var owner = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        // no follow relationship established

        await CreateTaskForTest(owner, title: "Private Task", visibility: TaskVisibility.Private);
        await CreateTaskForTest(owner, title: "Friends Task", visibility: TaskVisibility.FriendsOnly);
        await CreateTaskForTest(owner, title: "Public Task", visibility: TaskVisibility.Public);

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(viewer.Id, owner.Id)).ToList();

        var returned = tasks.ShouldHaveSingleItem();
        returned.Title.ShouldBe("Public Task");
    }

    [Fact]
    public async Task GetAllTasksAsync_OneWayFollowOnly_TreatedAsNonFriend_ReturnsOnlyPublic()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var owner = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Follow.Add(new Follow { FollowerId = viewer.Id, FollowedId = owner.Id });
            await context.SaveChangesAsync();
        }

        await CreateTaskForTest(owner, title: "Friends Task", visibility: TaskVisibility.FriendsOnly);
        await CreateTaskForTest(owner, title: "Public Task", visibility: TaskVisibility.Public);

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(viewer.Id, owner.Id)).ToList();

        var returned = tasks.ShouldHaveSingleItem();
        returned.Title.ShouldBe("Public Task");
    }

    [Fact]
    public async Task GetAllTasksAsync_ViewingProfileWithNoVisibleTasks_ReturnsEmpty()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var owner = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await CreateTaskForTest(owner, title: "Private Task", visibility: TaskVisibility.Private);

        var tasks = await ServiceUnderTest.GetAllTasksAsync(viewer.Id, owner.Id);

        tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTasksAsync_ExcludesChildTasksFromTopLevelResults()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var parent = await CreateTaskForTest(user, title: "Parent");
        await CreateTaskForTest(user, title: "Child", parentTaskId: parent.Id);

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(user.Id, null)).ToList();

        var returned = tasks.ShouldHaveSingleItem();
        returned.Title.ShouldBe("Parent");
    }

    [Fact]
    public async Task GetAllTasksAsync_IncludesNestedChildrenTwoLevelsDeep()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var root = await CreateTaskForTest(user, title: "Root");
        var child = await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Child",
            ParentTaskId = root.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });
        await ServiceUnderTest.CreateNewTaskAsync(user.Id, new CreateTaskRequest
        {
            Title = "Grandchild",
            ParentTaskId = child.Id,
            DueDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(2)),
            ItemStatus = TaskItemStatus.Todo
        });

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(user.Id, null)).ToList();

        var returnedRoot = tasks.ShouldHaveSingleItem();
        var returnedChild = returnedRoot.Children.ShouldHaveSingleItem();
        returnedChild.Title.ShouldBe("Child");
        var returnedGrandchild = returnedChild.Children.ShouldHaveSingleItem();
        returnedGrandchild.Title.ShouldBe("Grandchild");
    }

    [Fact]
    public async Task GetAllTasksAsync_MultipleTasks_OrderedByDueDateThenDueTime()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(user, title: "Later Date", dueDaysFromNow: 5);
        await CreateTaskForTest(user, title: "Earlier Date, Later Time", dueDaysFromNow: 2, dueTime: new TimeOnly(18, 0));
        await CreateTaskForTest(user, title: "Earlier Date, Earlier Time", dueDaysFromNow: 2, dueTime: new TimeOnly(8, 0));

        var tasks = (await ServiceUnderTest.GetAllTasksAsync(user.Id, null)).ToList();

        tasks.Select(t => t.Title).ShouldBe(new[]
        {
        "Earlier Date, Earlier Time",
        "Earlier Date, Later Time",
        "Later Date"
    });
    }

    private async Task<TaskItem> AddTaskDirectlyToDbAsync(
    User user,
    DateOnly dueDate,
    TimeOnly dueTime,
    TaskItemStatus status,
    TaskVisibility visibility = TaskVisibility.Private)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var task = new TaskItem
        {
            UserId = user.Id,
            Title = "Direct Task",
            DueDate = dueDate,
            DueTime = dueTime,
            ItemStatus = status,
            Visibility = visibility,
            CreatedAt = TestNow,
            Order = 0,
            Depth = 0,
        };

        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_NoTasks_ReturnsAllZero()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.Todo.ShouldBe(0);
        result.InProgress.ShouldBe(0);
        result.Done.ShouldBe(0);
        result.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_OwnProfile_CountsAllVisibilitiesRegardlessOfFriendStatus()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(user, status: TaskItemStatus.Todo, visibility: TaskVisibility.Private);
        await CreateTaskForTest(user, status: TaskItemStatus.InProgress, visibility: TaskVisibility.FriendsOnly);
        await CreateTaskForTest(user, status: TaskItemStatus.Done, visibility: TaskVisibility.Public);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.Todo.ShouldBe(1);
        result.InProgress.ShouldBe(1);
        result.Done.ShouldBe(1);
        result.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_DifferentUser_PublicTaskCounted()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var target = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(target, status: TaskItemStatus.Todo, visibility: TaskVisibility.Public);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(viewer.Id, target.Id);

        result.Todo.ShouldBe(1);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_DifferentUser_PrivateTaskExcluded()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var target = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(target, status: TaskItemStatus.Todo, visibility: TaskVisibility.Private);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(viewer.Id, target.Id);

        result.Todo.ShouldBe(0);
        result.InProgress.ShouldBe(0);
        result.Done.ShouldBe(0);
        result.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_DifferentUser_FriendsOnly_MutualFriend_Counted()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var target = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await AddMutualFollowAsync(viewer, target);
        await CreateTaskForTest(target, status: TaskItemStatus.InProgress, visibility: TaskVisibility.FriendsOnly);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(viewer.Id, target.Id);

        result.InProgress.ShouldBe(1);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_DifferentUser_FriendsOnly_NotFriend_Excluded()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var target = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(target, status: TaskItemStatus.InProgress, visibility: TaskVisibility.FriendsOnly);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(viewer.Id, target.Id);

        result.InProgress.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_DifferentUser_FriendsOnly_OneWayFollow_Excluded()
    {
        var viewer = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var target = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Follow.Add(new Follow { FollowerId = viewer.Id, FollowedId = target.Id });
            await context.SaveChangesAsync();
        }

        await CreateTaskForTest(target, status: TaskItemStatus.Todo, visibility: TaskVisibility.FriendsOnly);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(viewer.Id, target.Id);

        result.Todo.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_MultipleTasksAcrossStatuses_BucketsCorrectly()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        await CreateTaskForTest(user, status: TaskItemStatus.Todo);
        await CreateTaskForTest(user, status: TaskItemStatus.Todo);
        await CreateTaskForTest(user, status: TaskItemStatus.InProgress);
        await CreateTaskForTest(user, status: TaskItemStatus.Done);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.Todo.ShouldBe(2);
        result.InProgress.ShouldBe(1);
        result.Done.ShouldBe(1);
        result.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_OverdueIncompleteTask_CountedAsOverdue()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var pastDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(-3));
        await AddTaskDirectlyToDbAsync(user, pastDate, new TimeOnly(12, 0), TaskItemStatus.InProgress);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.Overdue.ShouldBe(1);
        result.InProgress.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_DoneTaskWithPastDueDate_ExcludedFromAllBuckets()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var pastDate = DateOnly.FromDateTime(TestNow.DateTime.AddDays(-3));
        await AddTaskDirectlyToDbAsync(user, pastDate, new TimeOnly(12, 0), TaskItemStatus.Done);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.Todo.ShouldBe(0);
        result.InProgress.ShouldBe(0);
        result.Done.ShouldBe(0);
        result.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_TaskDueLaterToday_CountedAsOnTrackNotOverdue()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var today = DateOnly.FromDateTime(TestNow.DateTime);
        var laterToday = TestNow.DateTime.AddMinutes(1);
        // Must go through the service here since due date/time is in the future relative to TestNow
        await CreateTaskForTest(user, dueDaysFromNow: 0, dueTime: TimeOnly.FromDateTime(laterToday), status: TaskItemStatus.InProgress);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.InProgress.ShouldBe(1);
        result.Overdue.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskStatusCountsAsync_TaskDueEarlierTodayIncomplete_CountedAsOverdue()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var today = DateOnly.FromDateTime(TestNow.DateTime);
        var earlierToday = TestNow.DateTime.AddMinutes(-1);
        await AddTaskDirectlyToDbAsync(user, today, TimeOnly.FromDateTime(earlierToday), TaskItemStatus.Todo);

        var result = await ServiceUnderTest.GetTaskStatusCountsAsync(user.Id, user.Id);

        result.Overdue.ShouldBe(1);
        result.Todo.ShouldBe(0);
    }

    [Fact]
    public async Task GetTaskByIdAsync_TaskHasReactions_ReturnsTaskWithReactionsIncluded()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await CreateTaskForTest(user);
        var reaction = await AddReactionToDatabaseAsync("thumbsup");
        await AddTaskReactionAsync(task, reaction, user);

        var fetched = await ServiceUnderTest.GetTaskByIdAsync(user.Id, task.Id);

        fetched.ShouldNotBeNull();
        var returnedReaction = fetched!.Reactions.ShouldHaveSingleItem();
        returnedReaction.UserId.ShouldBe(user.Id);
        returnedReaction.Reaction.ShouldNotBeNull();
        returnedReaction.Reaction!.Name.ShouldBe("thumbsup");
    }

    [Fact]
    public async Task GetTaskByIdAsync_NoReactions_ReturnsEmptyReactionsCollection()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await CreateTaskForTest(user);

        var fetched = await ServiceUnderTest.GetTaskByIdAsync(user.Id, task.Id);

        fetched.ShouldNotBeNull();
        fetched!.Reactions.ShouldBeEmpty();
    }
}
