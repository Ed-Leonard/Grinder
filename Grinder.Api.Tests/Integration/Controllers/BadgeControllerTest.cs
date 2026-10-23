using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using Grinder.Api.Models.DTOs;
using Shouldly;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Grinder.Api.Tests.Integration.Controllers;

public class BadgeControllerTest : BaseIntegrationTestFixture
{
    private int _userId;
    public BadgeControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

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
        _userId = existingUser.Id;

        var loginUserRequest = new LoginRequest("test@test.com", "Password123!");
        await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }

    private async Task SeedBadges()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Badges.AddRange(
            new Badge
            {
                Name = "The Grinder",
                Icon = "grinder",
                Description = "Awarded for having 10 tasks in Done status.",
                CriteriaKey = "TasksDone",
                Threshold = 10
            }
        );
        await context.SaveChangesAsync();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();
        await SeedBadges();
    }

    private async Task<Badge> GetSeededBadges()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        return context.Badges.First();
    }

    private async Task AwardBadgeToUser(int userId, int badgeId)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.UserBadges.Add(new UserBadge
        {
            UserId = userId,
            BadgeId = badgeId,
            EarnedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetBadges_ReturnsOkWithBadges()
    {
        var response = await HttpClient.GetAsync("/api/badge");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var badges = await response.Content.ReadFromJsonAsync<List<BadgeResponse>>();
        badges.ShouldNotBeNull();
        badges.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SelectBadge_BadgeEarned_ReturnsOk()
    {
        var badge = await GetSeededBadges();
        await AwardBadgeToUser(_userId, badge.Id);

        var response = await HttpClient.PutAsJsonAsync("/api/badge/select", new SelectBadgeRequest(badge.Id));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SelectBadge_BadgeNotEarned_ReturnsBadRequest()
    {
        var badge = await GetSeededBadges();

        var response = await HttpClient.PutAsJsonAsync("/api/badge/select", new SelectBadgeRequest(badge.Id));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

}
