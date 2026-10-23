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

public class ClipControllerTest : BaseIntegrationTestFixture
{
    public ClipControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

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
        _stranger = await CreateUserAsync("stranger@test.com", "Stranger User");

        context.TaskItems.Add(new TaskItem
        {
            Title = "Stranger's public clip",
            Description = "description",
            Visibility = TaskVisibility.Public,
            IsClip = true,
            UserId = _stranger.Id
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetClips_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/clip");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClips_Authenticated_ReturnsOkWithPublicClipsFromAnyone()
    {
        var response = await HttpClient.GetAsync("/api/clip");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var clips = await response.Content.ReadFromJsonAsync<ClipPageDto>();
        clips.ShouldNotBeNull();
        var item = clips!.Items.ShouldHaveSingleItem();
        item.Title.ShouldBe("Stranger's public clip");
        item.UserId.ShouldBe(_stranger.Id);
    }

    [Fact]
    public async Task GetClips_NoQueryParams_DefaultsToFirstPage()
    {
        var response = await HttpClient.GetAsync("/api/clip");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var clips = await response.Content.ReadFromJsonAsync<ClipPageDto>();
        clips.ShouldNotBeNull();
        clips!.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetClips_WithLimit_ReturnsAtMostLimitItems()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            for (var i = 0; i < 5; i++)
            {
                context.TaskItems.Add(new TaskItem
                {
                    Title = $"Extra clip {i}",
                    Visibility = TaskVisibility.Public,
                    IsClip = true,
                    UserId = _stranger.Id
                });
            }
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/clip?page=1&limit=2");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var clips = await response.Content.ReadFromJsonAsync<ClipPageDto>();
        clips.ShouldNotBeNull();
        clips!.Items.Count.ShouldBe(2);
        clips.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetClips_OwnClip_ExcludedFromResponse()
    {
        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.Add(new TaskItem
            {
                Title = "My own public clip",
                Visibility = TaskVisibility.Public,
                IsClip = true,
                UserId = _user.Id
            });
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/clip");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var clips = await response.Content.ReadFromJsonAsync<ClipPageDto>();
        clips.ShouldNotBeNull();
        clips!.Items.ShouldNotContain(item => item.UserId == _user.Id);
    }

    [Fact]
    public async Task GetClips_NoClips_ReturnsEmptyItems()
    {
        var newUser = await CreateUserAsync("nobody@test.com", "Nobody User");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            await context.TaskItems
                .Where(t => t.IsClip)
                .ExecuteDeleteAsync();
        }

        var loginResponse = await HttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("nobody@test.com", "Password123!"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await HttpClient.GetAsync("/api/clip");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var clips = await response.Content.ReadFromJsonAsync<ClipPageDto>();
        clips.ShouldNotBeNull();
        clips!.Items.ShouldBeEmpty();
    }
}
