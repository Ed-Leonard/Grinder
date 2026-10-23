using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class ReactionServiceTest : BaseIntegrationTestFixture
{
    private IReactionService ServiceUnderTest => ServiceProvider.GetRequiredService<IReactionService>();

    public ReactionServiceTest(WebApplicationFactory<Program> webAppFactory)
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

    private async Task<TaskItem> AddTaskToDatabaseAsync(User user, string title = "Title")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var task = new TaskItem
        {
            Title = title,
            ItemStatus = TaskItemStatus.Todo,
            CreatedAt = TestNow,
            UserId = user.Id,
            Visibility = TaskVisibility.Public
        };

        context.TaskItems.Add(task);
        await context.SaveChangesAsync();

        return task;
    }

    private async Task<Reaction> AddReactionToDatabaseAsync(string name = "thumbsup")
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var reaction = new Reaction
        {
            Name = name,
            FileName = $"{name}.png"
        };

        context.Reactions.Add(reaction);
        await context.SaveChangesAsync();

        return reaction;
    }

    [Fact]
    public async Task GetReactionsAsync_NoReactionsInDb_ReturnsEmptyCollection()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");

        var reactions = await ServiceUnderTest.GetReactionsAsync();

        reactions.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReactionsAsync_ReactionsExist_ReturnsAllReactions()
    {
        await AddReactionToDatabaseAsync("thumbsup");
        await AddReactionToDatabaseAsync("heart");

        var reactions = (await ServiceUnderTest.GetReactionsAsync()).ToList();

        reactions.Count.ShouldBe(2);
        reactions.Select(r => r.Name).ShouldContain("thumbsup");
        reactions.Select(r => r.Name).ShouldContain("heart");
    }

    [Fact]
    public async Task ToggleReactionAsync_NewReaction_AddsReactionAndReturnsCountOne()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await AddTaskToDatabaseAsync(user);
        var reaction = await AddReactionToDatabaseAsync();

        var result = await ServiceUnderTest.ToggleReactionAsync(task.Id, reaction.Id, user.Id);

        result.Count.ShouldBe(1);
        result.ReactedByCurrentUser.ShouldBeTrue();
        result.Reaction.Id.ShouldBe(reaction.Id);
        result.Reaction.Name.ShouldBe(reaction.Name);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var stored = await context.TaskReactions
            .SingleAsync(tr => tr.TaskItemId == task.Id && tr.ReactionId == reaction.Id);
        stored.UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task ToggleReactionAsync_ExistingReactionFromSameUser_RemovesReactionAndReturnsCountZero()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await AddTaskToDatabaseAsync(user);
        var reaction = await AddReactionToDatabaseAsync();

        await ServiceUnderTest.ToggleReactionAsync(task.Id, reaction.Id, user.Id);
        var result = await ServiceUnderTest.ToggleReactionAsync(task.Id, reaction.Id, user.Id);

        result.Count.ShouldBe(0);
        result.ReactedByCurrentUser.ShouldBeFalse();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var exists = await context.TaskReactions
            .AnyAsync(tr => tr.TaskItemId == task.Id && tr.ReactionId == reaction.Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ToggleReactionAsync_RemovingOwnReaction_DoesNotAffectOtherUsersReaction()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var otherUser = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await AddTaskToDatabaseAsync(user);
        var reaction = await AddReactionToDatabaseAsync();

        await ServiceUnderTest.ToggleReactionAsync(task.Id, reaction.Id, otherUser.Id);
        await ServiceUnderTest.ToggleReactionAsync(task.Id, reaction.Id, user.Id);
        var result = await ServiceUnderTest.ToggleReactionAsync(task.Id, reaction.Id, user.Id);

        result.Count.ShouldBe(1);
        result.ReactedByCurrentUser.ShouldBeFalse();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var remaining = await context.TaskReactions
            .SingleAsync(tr => tr.TaskItemId == task.Id && tr.ReactionId == reaction.Id);
        remaining.UserId.ShouldBe(otherUser.Id);
    }

    [Fact]
    public async Task ToggleReactionAsync_ReactionDoesNotExist_ThrowsKeyNotFoundException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var task = await AddTaskToDatabaseAsync(user);

        await Should.ThrowAsync<KeyNotFoundException>(() =>
            ServiceUnderTest.ToggleReactionAsync(task.Id, 999999, user.Id)
        );

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskReactions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ToggleReactionAsync_TaskDoesNotExist_ThrowsKeyNotFoundException()
    {
        var user = await AddUserToDatabaseAsync(Guid.NewGuid() + "@example.com");
        var reaction = await AddReactionToDatabaseAsync();

        await Should.ThrowAsync<KeyNotFoundException>(() =>
            ServiceUnderTest.ToggleReactionAsync(999999, reaction.Id, user.Id)
        );

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskReactions.ShouldBeEmpty();
    }

}