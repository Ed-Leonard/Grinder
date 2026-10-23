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

public class WrappedControllerTest : BaseIntegrationTestFixture
{
    public WrappedControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

    private User _user = null!;

    private async Task RegisterAndLogin()
    {
        var user = new User
        {
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var loginRequest = new LoginRequest("test@test.com", "Password123!");
        await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    private async Task SetUserCreatedAt(DateTime createdAt)
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();
        var user = await db.Users.FirstAsync(u => u.Id == _user.Id);
        user.CreatedAt = createdAt;
        await db.SaveChangesAsync();
    }

    private async Task AddCompletedTaskAsync(DateTimeOffset completedAt)
    {
        await using var db = await DbContextFactory.CreateDbContextAsync();
        db.TaskItems.Add(new TaskItem
        {
            UserId = _user.Id,
            Title = "Task",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            ItemStatus = TaskItemStatus.Done,
            CreatedAt = completedAt.AddDays(-5),
            UpdatedAt = completedAt,
            CompletedAt = completedAt
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetGenerateEligibility_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/wrapped/constant-eligibility");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetGenerateEligibility_EligibleForCurrentYear_ReturnsTrue()
    {
        await SetUserCreatedAt(DateTime.UtcNow.AddMonths(-6));
        await AddCompletedTaskAsync(DateTimeOffset.UtcNow.AddDays(-10));

        var response = await HttpClient.GetAsync("/api/wrapped/constant-eligibility");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<WrappedConstantEligibilityResponse>();
        body.ShouldNotBeNull();
        body!.CurrentYearEligible.ShouldBeTrue();
        body.CurrentYear.ShouldBe(DateTime.UtcNow.Year);
    }

    [Fact]
    public async Task GetGenerateEligibility_AccountTooNew_CurrentYearIneligible()
    {
        await SetUserCreatedAt(DateTime.UtcNow.AddDays(-7));
        await AddCompletedTaskAsync(DateTimeOffset.UtcNow.AddDays(-2));

        var response = await HttpClient.GetAsync("/api/wrapped/constant-eligibility");
        var body = await response.Content.ReadFromJsonAsync<WrappedConstantEligibilityResponse>();

        body!.CurrentYearEligible.ShouldBeFalse();
    }

    [Fact]
    public async Task GetGenerateEligibility_TaskCompletedLastYear_PreviousYearEligible()
    {
        await SetUserCreatedAt(DateTime.UtcNow.AddMonths(-6));
        var lastYear = DateTime.UtcNow.Year - 1;
        await AddCompletedTaskAsync(new DateTimeOffset(lastYear, 6, 15, 0, 0, 0, TimeSpan.Zero));

        var response = await HttpClient.GetAsync("/api/wrapped/constant-eligibility");
        var body = await response.Content.ReadFromJsonAsync<WrappedConstantEligibilityResponse>();

        body!.PreviousYearEligible.ShouldBeTrue();
        body.PreviousYear.ShouldBe(lastYear);
    }

    [Fact]
    public async Task GetGenerateEligibility_NoTaskLastYear_PreviousYearIneligible()
    {
        await SetUserCreatedAt(DateTime.UtcNow.AddMonths(-6));
        var response = await HttpClient.GetAsync("/api/wrapped/constant-eligibility");
        var body = await response.Content.ReadFromJsonAsync<WrappedConstantEligibilityResponse>();

        body!.PreviousYearEligible.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStats_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/wrapped/stats?year=2026");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStats_NoCompletedTaskForYear_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync($"/api/wrapped/stats?year={DateTime.UtcNow.Year - 5}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStats_ValidYear_ReturnsStats()
    {
        await SetUserCreatedAt(DateTime.UtcNow.AddMonths(-6));
        var year = DateTime.UtcNow.Year;
        await AddCompletedTaskAsync(new DateTimeOffset(year, 3, 1, 0, 0, 0, TimeSpan.Zero));

        var response = await HttpClient.GetAsync($"/api/wrapped/stats?year={year}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<WrappedStatsResponse>();
        stats.ShouldNotBeNull();
        stats!.TasksCompleted.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetStats_YearOutsideCurrentOrPrevious_ReturnsNotFoundWithoutQueryingEligibility()
    {
        await SetUserCreatedAt(DateTime.UtcNow.AddMonths(-6));
        await AddCompletedTaskAsync(new DateTimeOffset(DateTime.UtcNow.Year - 2, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var response = await HttpClient.GetAsync($"/api/wrapped/stats?year={DateTime.UtcNow.Year - 2}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}