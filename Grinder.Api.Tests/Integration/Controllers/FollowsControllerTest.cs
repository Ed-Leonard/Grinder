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

public class FollowsControllerTest : BaseIntegrationTestFixture
{
    private sealed class RelationshipStatusesPayload
    {
        public Dictionary<int, string> Relationships { get; init; } = new();
    }

    public FollowsControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

    private User _user = null!;
    private User _sender = null!;

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
        _sender = await CreateUserAsync("sender@test.com", "Sender User");
    }

    [Fact]
    public async Task FollowUser_UserExist_ShouldReturnOK()
    {
        var response = await HttpClient.PostAsync($"/api/follows/{_sender.Id}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FollowUser_UserDoesNotExist_ShouldReturnBadRequest()
    {
        var response = await HttpClient.PostAsync($"/api/follows/999", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FollowUser_AlreadyFollowing_ShouldReturnBadRequest()
    {
        await HttpClient.PostAsync($"/api/follows/{_sender.Id}", null);
        var response = await HttpClient.PostAsync($"/api/follows/{_sender.Id}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnfollowUser_UserExist_ShouldReturnOK()
    {
        await HttpClient.PostAsync($"/api/follows/{_sender.Id}", null);
        var response = await HttpClient.DeleteAsync($"/api/follows/{_sender.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnfollowUser_UserDoesNotExist_ShouldReturnBadRequest()
    {
        var response = await HttpClient.DeleteAsync($"/api/follows/999");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetRelationshipStatuses_UserExist_ShouldReturnFollowingStatus()
    {
        await HttpClient.PostAsync($"/api/follows/{_sender.Id}", null);
        var response = await HttpClient.GetAsync($"/api/follows/relationship-statuses?userIds={_sender.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<RelationshipStatusesPayload>();
        content.ShouldNotBeNull();
        content!.Relationships[_sender.Id].ShouldBe("Following");
    }

    [Fact]
    public async Task GetRelationshipStatuses_MultipleUsers_ShouldReturnStatusesForEachUser()
    {
        var otherUser = await CreateUserAsync("other@test.com", "Other User");

        await HttpClient.PostAsync($"/api/follows/{_sender.Id}", null);

        var response = await HttpClient.GetAsync($"/api/follows/relationship-statuses?userIds={_sender.Id}&userIds={otherUser.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<RelationshipStatusesPayload>();
        content.ShouldNotBeNull();
        content!.Relationships[_sender.Id].ShouldBe("Following");
        content.Relationships[otherUser.Id].ShouldBe("Follow");
    }
}
