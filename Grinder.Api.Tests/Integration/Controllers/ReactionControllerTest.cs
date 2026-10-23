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

public class ReactionControllerTest : BaseIntegrationTestFixture
{
    public ReactionControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

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
        await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }

    // Runs before every test so we don't need to call RegisterAndLogin every test
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    private async Task<TaskItem> AddSampleTaskToDatabase(User user)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Title = "Test Task",
            Description = "Description",
            ItemStatus = TaskItemStatus.Todo,
            CreatedAt = TestNow,
            UserId = user.Id
        };
        dbContext.TaskItems.Add(task);
        await dbContext.SaveChangesAsync();
        return task;
    }

    private async Task<IReadOnlyList<Reaction>> AddSampleReactionsToDatabase(int count)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var reactions = new List<Reaction>();
        for (var i = 0; i < count; i++)
        {
            reactions.Add(new Reaction
            {
                Name = $"reaction_{i}",
                FileName = $"reaction_{i}.png"
            });
        }
        dbContext.Reactions.AddRange(reactions);
        await dbContext.SaveChangesAsync();
        return reactions;
    }

    [Fact]
    public async Task GetReactions_ReactionsExist_ReturnsAllReactions()
    {
        var reactions = await AddSampleReactionsToDatabase(3);

        var response = await HttpClient.GetAsync("/api/reactions");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<List<Reaction>>();
        returned.ShouldNotBeNull();
        returned.Count.ShouldBe(3);
        returned.Select(r => r.Name)
            .ShouldBeEquivalentTo(reactions.Select(r => r.Name));
    }

    [Fact]
    public async Task GetReactions_NoReactionsExist_ReturnsEmptyList()
    {
        var response = await HttpClient.GetAsync("/api/reactions");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<List<Reaction>>();
        returned.ShouldNotBeNull();
        returned.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToggleReaction_NewReaction_ReturnsOkAndAddsReaction()
    {
        var task = await AddSampleTaskToDatabase(_user);
        var reactions = await AddSampleReactionsToDatabase(1);

        var response = await HttpClient.PostAsync($"/api/tasks/{task.Id}/reactions/{reactions[0].Id}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<ReactionResponse>();
        returned.ShouldNotBeNull();
        returned!.Count.ShouldBe(1);
        returned.ReactedByCurrentUser.ShouldBeTrue();
        returned.Reaction.Id.ShouldBe(reactions[0].Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var stored = await context.TaskReactions
            .SingleAsync(tr => tr.TaskItemId == task.Id && tr.ReactionId == reactions[0].Id);
        stored.UserId.ShouldBe(_user.Id);
    }

    [Fact]
    public async Task ToggleReaction_ReactedByCurrentUser_SecondCallRemovesReaction()
    {
        var task = await AddSampleTaskToDatabase(_user);
        var reactions = await AddSampleReactionsToDatabase(1);

        await HttpClient.PostAsync($"/api/tasks/{task.Id}/reactions/{reactions[0].Id}", null);

        var response = await HttpClient.PostAsync($"/api/tasks/{task.Id}/reactions/{reactions[0].Id}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var returned = await response.Content.ReadFromJsonAsync<ReactionResponse>();
        returned.ShouldNotBeNull();
        returned!.Count.ShouldBe(0);
        returned.ReactedByCurrentUser.ShouldBeFalse();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var exists = await context.TaskReactions
            .AnyAsync(tr => tr.TaskItemId == task.Id && tr.ReactionId == reactions[0].Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleReaction_TaskDoesNotExist_ReturnsBadRequest()
    {
        var reactions = await AddSampleReactionsToDatabase(1);

        var response = await HttpClient.PostAsync($"/api/tasks/999999/reactions/{reactions[0].Id}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleReaction_ReactionDoesNotExist_ReturnsBadRequest()
    {
        var task = await AddSampleTaskToDatabase(_user);

        var response = await HttpClient.PostAsync($"/api/tasks/{task.Id}/reactions/999999", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ToggleReaction_Unauthenticated_ReturnsUnauthorized()
    {
        var task = await AddSampleTaskToDatabase(_user);
        var reactions = await AddSampleReactionsToDatabase(1);

        var response = await UnauthenticatedClient.PostAsync($"/api/tasks/{task.Id}/reactions/{reactions[0].Id}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}