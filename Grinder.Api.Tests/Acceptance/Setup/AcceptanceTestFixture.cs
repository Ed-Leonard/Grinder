using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Tests.Integration;
using Reqnroll;
using Grinder.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using System.Net.Http.Json;
using Grinder.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grinder.Api.Tests.Acceptance.Setup;

[Binding]
public class AcceptanceTestFixture : BaseIntegrationTestFixture
{
    public int? CurrentUserId { get; set; }
    public int? currentTaskId { get; set; }
    public HttpResponseMessage? LastResponse { get; set; }
    public DateTimeOffset AcceptanceTestNow { get; set; } = DateTimeOffset.UtcNow;
    public new HttpClient HttpClient => base.HttpClient;
    public new IDbContextFactory<DatabaseContext> DbContextFactory => base.DbContextFactory;
    public new IServiceProvider ServiceProvider => base.ServiceProvider;
    public FakeEmailService EmailService { get; } = new();
    public FakeLiveKitRoomService LiveKitRoomService { get; } = new();

    public AcceptanceTestFixture(WebApplicationFactory<Program> factory) : base(factory, handleCookies: true)
    {
    }

    public async Task LoginAsTestUser()
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
        user.PasswordHash = passwordHasher.HashPassword(user, user.PasswordHash);

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        CurrentUserId = user.Id;

        var loginRequest = new LoginRequest("test@test.com", "Password123!");
        var response = await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();
    }

    public async Task RegisterAsTestUser()
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
        user.PasswordHash = passwordHasher.HashPassword(user, user.PasswordHash);

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    protected override void ConfigureCustomServicesForTesting(IServiceCollection services)
    {
        services.RemoveAll<IEmailService>();
        services.AddSingleton<IEmailService>(EmailService);

        services.RemoveAll<ILiveKitRoomService>();
        services.AddSingleton<ILiveKitRoomService>(LiveKitRoomService);
    }

    [BeforeScenario]
    public async Task ScenarioSetup()
    {
        await InitializeAsync();
        CurrentUserId = null;
        EmailService.SentEmails.Clear();
        LiveKitRoomService.ActiveRooms.Clear();
    }

    [AfterScenario]
    public async Task ScenarioTeardown()
    {
        await DisposeAsync();
    }

    public class FakeEmailService : IEmailService
    {
        public List<(string To, string Subject, string Body)> SentEmails { get; } = new();

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            SentEmails.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    public class FakeLiveKitRoomService : ILiveKitRoomService
    {
        public Dictionary<string, int> ActiveRooms { get; } = new();

        public Task<IReadOnlyDictionary<string, int>> GetActiveRoomViewerCountsAsync()
        {
            return Task.FromResult<IReadOnlyDictionary<string, int>>(ActiveRooms);
        }

        public Task DeleteRoomAsync(string roomName)
        {
            ActiveRooms.Remove(roomName);
            return Task.CompletedTask;
        }
    }

    public void SetNow(DateTimeOffset now)
    {
        FakeTimeProvider.SetUtcNow(now);
        AcceptanceTestNow = now;
    }
}
