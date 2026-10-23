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

public class ExploreControllerTest : BaseIntegrationTestFixture
{
    public ExploreControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

    private User _user = null!;
    private User _stranger = null!;

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
        // Deliberately NOT followed by _user - explore surfaces content independent of follows.
        _stranger = await CreateUserAsync("stranger@test.com", "Stranger User");

        context.TaskItems.Add(new TaskItem
        {
            Title = "Stranger's public task",
            Description = "description",
            Visibility = TaskVisibility.Public,
            ItemStatus = TaskItemStatus.InProgress,
            UserId = _stranger.Id
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetExploreFeed_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/explore");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExploreFeed_Authenticated_ReturnsOkWithNonFollowedUsersTasks()
    {
        var response = await HttpClient.GetAsync("/api/explore");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var explore = await response.Content.ReadFromJsonAsync<ExplorePage>();
        explore.ShouldNotBeNull();
        var item = explore!.Items.ShouldHaveSingleItem();
        item.Title.ShouldBe("Stranger's public task");
    }

    [Fact]
    public async Task GetExploreFeed_NoQueryParams_DefaultsToFirstPage()
    {
        var response = await HttpClient.GetAsync("/api/explore");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var explore = await response.Content.ReadFromJsonAsync<ExplorePage>();
        explore.ShouldNotBeNull();
        explore!.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetExploreFeed_WithLimit_ReturnsAtMostLimitItems()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Title = $"Extra task {i}",
                    Visibility = TaskVisibility.Public,
                    UserId = _stranger.Id
                });
            }
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/explore?page=1&limit=2");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var explore = await response.Content.ReadFromJsonAsync<ExplorePage>();
        explore.ShouldNotBeNull();
        explore!.Items.Count.ShouldBe(2);
        explore.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetExploreFeed_SecondPage_ReturnsDifferentItemsThanFirstPage()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Title = $"Extra task {i}",
                    Visibility = TaskVisibility.Public,
                    UserId = _stranger.Id
                });
            }
            await context.SaveChangesAsync();
        }

        var firstPageResponse = await HttpClient.GetAsync("/api/explore?page=1&limit=2");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<ExplorePage>();

        var secondPageResponse = await HttpClient.GetAsync("/api/explore?page=2&limit=2");
        secondPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<ExplorePage>();

        secondPage.ShouldNotBeNull();
        secondPage!.Page.ShouldBe(2);
        secondPage.Items.ShouldNotBe(firstPage!.Items);
    }

    [Fact]
    public async Task GetExploreFeed_ExcludesPrivateTasks()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.Add(new TaskItem
            {
                Title = "Private task",
                Visibility = TaskVisibility.Private,
                UserId = _stranger.Id
            });
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/explore");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var explore = await response.Content.ReadFromJsonAsync<ExplorePage>();
        explore.ShouldNotBeNull();
        explore!.Items.ShouldAllBe(item => item.Title != "Private task");
    }

    [Fact]
    public async Task GetExploreFeed_ExcludesCurrentUsersOwnTasks()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.Add(new TaskItem
            {
                Title = "My own task",
                Visibility = TaskVisibility.Public,
                UserId = _user.Id
            });
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/explore");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var explore = await response.Content.ReadFromJsonAsync<ExplorePage>();
        explore.ShouldNotBeNull();
        explore!.Items.ShouldAllBe(item => item.Title != "My own task");
    }
}
