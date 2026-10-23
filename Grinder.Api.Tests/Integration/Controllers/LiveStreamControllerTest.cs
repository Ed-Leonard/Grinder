using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class LiveStreamControllerTest : BaseIntegrationTestFixture
{
    public LiveStreamControllerTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory, handleCookies: true) { }

    protected override void ConfigureCustomServicesForTesting(IServiceCollection services)
    {
        services.RemoveAll<ILiveKitRoomService>();
        var roomServiceMock = Substitute.For<ILiveKitRoomService>();
        roomServiceMock
            .GetActiveRoomViewerCountsAsync()
            .Returns(Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>()));
        services.AddSingleton(roomServiceMock);
    }

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

    private async Task<User> CreateUserAsync(string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "placeholder",
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

    private async Task<LiveStream> AddLiveAsync(User host, TaskVisibility visibility, string roomName)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Title = "task",
            Description = "description",
            Visibility = visibility,
            UserId = host.Id
        };
        dbContext.TaskItems.Add(task);
        await dbContext.SaveChangesAsync();

        var live = new LiveStream
        {
            Title = "live",
            RoomName = roomName,
            TaskId = task.Id,
            UserId = host.Id,
            Visibility = visibility
        };
        dbContext.LiveStreams.Add(live);
        await dbContext.SaveChangesAsync();
        return live;
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    [Fact]
    public async Task GetActive_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/livestreams/active");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetActive_NoActiveRooms_ReturnsOkWithEmptyItems()
    {
        var response = await HttpClient.GetAsync("/api/livestreams/active");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<LiveStreamPageResponse>();
        page.ShouldNotBeNull();
        page!.Items.ShouldBeEmpty();
        page.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetActive_PublicLiveExists_ReturnsIt()
    {
        var streamer = await CreateUserAsync("streamer@test.com", "Streamer");
        var live = await AddLiveAsync(streamer, TaskVisibility.Public, "room-1");

        var roomService = ServiceProvider.GetRequiredService<ILiveKitRoomService>();
        roomService
            .GetActiveRoomViewerCountsAsync()
            .Returns(Task.FromResult<IReadOnlyDictionary<string, int>>(
                new Dictionary<string, int> { [live.RoomName] = 7 }));

        var response = await HttpClient.GetAsync("/api/livestreams/active");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<LiveStreamPageResponse>();
        var item = page!.Items.ShouldHaveSingleItem();
        item.RoomName.ShouldBe("room-1");
        item.ViewerCount.ShouldBe(7);
        item.Streamer.Id.ShouldBe(streamer.Id);
    }

    [Fact]
    public async Task GetActive_FriendsOnlyLiveFromNonFriend_ExcludedFromList()
    {
        var streamer = await CreateUserAsync("streamer@test.com", "Streamer");
        var live = await AddLiveAsync(streamer, TaskVisibility.FriendsOnly, "room-1");

        var roomService = ServiceProvider.GetRequiredService<ILiveKitRoomService>();
        roomService
            .GetActiveRoomViewerCountsAsync()
            .Returns(Task.FromResult<IReadOnlyDictionary<string, int>>(
                new Dictionary<string, int> { [live.RoomName] = 3 }));

        var response = await HttpClient.GetAsync("/api/livestreams/active");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<LiveStreamPageResponse>();
        page!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetActive_ExcludeQueryParam_OmitsThoseRoomsFromResponse()
    {
        var streamer = await CreateUserAsync("streamer@test.com", "Streamer");
        var live = await AddLiveAsync(streamer, TaskVisibility.Public, "room-1");

        var roomService = ServiceProvider.GetRequiredService<ILiveKitRoomService>();
        roomService
            .GetActiveRoomViewerCountsAsync()
            .Returns(Task.FromResult<IReadOnlyDictionary<string, int>>(
                new Dictionary<string, int> { [live.RoomName] = 3 }));

        var response = await HttpClient.GetAsync($"/api/livestreams/active?exclude={live.RoomName}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await response.Content.ReadFromJsonAsync<LiveStreamPageResponse>();
        page!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task EndLive_OwnStream_DeletesItsLiveKitRoom()
    {
        var live = await AddLiveAsync(_user, TaskVisibility.Public, "room-to-end");

        var response = await HttpClient.DeleteAsync($"/api/livestreams/{live.RoomName}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var roomService = ServiceProvider.GetRequiredService<ILiveKitRoomService>();
        await roomService.Received(1).DeleteRoomAsync(live.RoomName);
    }

    [Fact]
    public async Task EndLive_AnotherUsersStream_ReturnsNotFound()
    {
        var owner = await CreateUserAsync("owner@test.com", "Owner");
        var live = await AddLiveAsync(owner, TaskVisibility.Public, "other-users-room");

        var response = await HttpClient.DeleteAsync($"/api/livestreams/{live.RoomName}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var roomService = ServiceProvider.GetRequiredService<ILiveKitRoomService>();
        await roomService.DidNotReceive().DeleteRoomAsync(live.RoomName);
    }

    [Fact]
    public async Task EndLive_Unauthed_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.DeleteAsync($"/api/livestreams/room");
        
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndLive_OwnedRoomDoesNotExist_ReturnsNotFoundAndDoesNotDelete()
    {
        var response = await HttpClient.DeleteAsync($"/api/livestreams/room-to-end");
        
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        
        var roomService = ServiceProvider.GetRequiredService<ILiveKitRoomService>();
        await roomService.DidNotReceive().DeleteRoomAsync(string.Empty);
    }
}
