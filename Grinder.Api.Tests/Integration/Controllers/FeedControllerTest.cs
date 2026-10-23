using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class FeedControllerTest : BaseIntegrationTestFixture
{
    public FeedControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

    private User _user = null!;
    private User _friend = null!;

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
        await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }

    private async Task<User> CreateUserAsync(string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
        _friend = await CreateUserAsync("friend@test.com", "Friend User");

        context.Follow.AddRange(
            new Follow { FollowerId = _user.Id, FollowedId = _friend.Id },
            new Follow { FollowerId = _friend.Id, FollowedId = _user.Id });

        context.TaskItems.Add(new TaskItem
        {
            Title = "Friend's public task",
            Description = "description",
            Visibility = TaskVisibility.Public,
            ItemStatus = TaskItemStatus.InProgress,
            UserId = _friend.Id
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetFeed_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/feed");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFeed_Authenticated_ReturnsOkWithFriendsTasks()
    {
        var response = await HttpClient.GetAsync("/api/feed");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var feed = await response.Content.ReadFromJsonAsync<FeedPageDto>();
        feed.ShouldNotBeNull();
        var item = feed!.Items.ShouldHaveSingleItem();
        item.Title.ShouldBe("Friend's public task");
        item.UserId.ShouldBe(_friend.Id);
        item.ItemStatus.ShouldBe(TaskItemStatus.InProgress);
    }

    [Fact]
    public async Task GetFeed_NoQueryParams_DefaultsToFirstPage()
    {
        var response = await HttpClient.GetAsync("/api/feed");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var feed = await response.Content.ReadFromJsonAsync<FeedPageDto>();
        feed.ShouldNotBeNull();
        feed!.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetFeed_WithLimit_ReturnsAtMostLimitItems()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Title = $"Extra task {i}",
                    Visibility = TaskVisibility.Public,
                    UserId = _friend.Id
                });
            }
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/feed?page=1&limit=2");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var feed = await response.Content.ReadFromJsonAsync<FeedPageDto>();
        feed.ShouldNotBeNull();
        feed!.Items.Count.ShouldBe(2);
        feed.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetFeed_NoFriends_ReturnsDiscoveryContentNotFriendsContent()
    {
        var lonelyUser = await CreateUserAsync("lonely@test.com", "Lonely User");
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.Add(new TaskItem
            {
                Title = "Should not appear",
                Visibility = TaskVisibility.Public,
                UserId = lonelyUser.Id
            });
            await context.SaveChangesAsync();
        }

        var loginResponse = await HttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("lonely@test.com", "Password123!"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await HttpClient.GetAsync("/api/feed");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var feed = await response.Content.ReadFromJsonAsync<FeedPageDto>();
        feed.ShouldNotBeNull();
        feed!.Items.ShouldAllBe(item => item.UserId != lonelyUser.Id);
    }

    [Fact]
    public async Task MarkTasksAsViewed_Unauthenticated_ReturnsUnauthorized()
    {
        var request = new MarkTasksViewedRequest { TaskItemIds = new List<int> { 1 } };

        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/feed/viewed", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkTasksAsViewed_EmptyList_ReturnsBadRequest()
    {
        var request = new MarkTasksViewedRequest { TaskItemIds = new List<int>() };

        var response = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkTasksAsViewed_NullTaskItemIds_ReturnsBadRequest()
    {
        var response = await HttpClient.PostAsJsonAsync("/api/feed/viewed", new { TaskItemIds = (List<int>?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkTasksAsViewed_MoreThan100Ids_ReturnsBadRequest()
    {
        var ids = Enumerable.Range(1, 101).ToList();
        var request = new MarkTasksViewedRequest { TaskItemIds = ids };

        var response = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MarkTasksAsViewed_Exactly100Ids_ReturnsNoContent()
    {
        List<int> ids;
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            var tasks = Enumerable.Range(0, 100)
                .Select(i => new TaskItem
                {
                    Title = $"Bulk task {i}",
                    Visibility = TaskVisibility.Public,
                    UserId = _friend.Id
                })
                .ToList();

            context.TaskItems.AddRange(tasks);
            await context.SaveChangesAsync();

            ids = tasks.Select(t => t.Id).ToList();
        }

        var request = new MarkTasksViewedRequest { TaskItemIds = ids };

        var response = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task MarkTasksAsViewed_ValidTaskId_ReturnsNoContentAndCreatesView()
    {
        TaskItem task;
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            task = new TaskItem
            {
                Title = "Task to view",
                Visibility = TaskVisibility.Public,
                UserId = _friend.Id
            };
            context.TaskItems.Add(task);
            await context.SaveChangesAsync();
        }

        var request = new MarkTasksViewedRequest { TaskItemIds = new List<int> { task.Id } };

        var response = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var view = await verifyContext.TaskViews
            .SingleOrDefaultAsync(v => v.UserId == _user.Id && v.TaskItemId == task.Id);
        view.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkTasksAsViewed_AlreadyViewedTask_UpdatesExistingViewInsteadOfDuplicating()
    {
        TaskItem task;
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            task = new TaskItem
            {
                Title = "Task to view twice",
                Visibility = TaskVisibility.Public,
                UserId = _friend.Id
            };
            context.TaskItems.Add(task);
            await context.SaveChangesAsync();
        }

        var request = new MarkTasksViewedRequest { TaskItemIds = new List<int> { task.Id } };

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var secondResponse = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var viewCount = await verifyContext.TaskViews
            .CountAsync(v => v.UserId == _user.Id && v.TaskItemId == task.Id);
        viewCount.ShouldBe(1);
    }

    [Fact]
    public async Task MarkTasksAsViewed_MultipleTaskIds_CreatesViewForEach()
    {
        TaskItem taskOne, taskTwo;
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            taskOne = new TaskItem { Title = "Task one", Visibility = TaskVisibility.Public, UserId = _friend.Id };
            taskTwo = new TaskItem { Title = "Task two", Visibility = TaskVisibility.Public, UserId = _friend.Id };
            context.TaskItems.AddRange(taskOne, taskTwo);
            await context.SaveChangesAsync();
        }

        var request = new MarkTasksViewedRequest { TaskItemIds = new List<int> { taskOne.Id, taskTwo.Id } };

        var response = await HttpClient.PostAsJsonAsync("/api/feed/viewed", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var viewedIds = await verifyContext.TaskViews
            .Where(v => v.UserId == _user.Id)
            .Select(v => v.TaskItemId)
            .ToListAsync();

        viewedIds.ShouldContain(taskOne.Id);
        viewedIds.ShouldContain(taskTwo.Id);
    }
}
