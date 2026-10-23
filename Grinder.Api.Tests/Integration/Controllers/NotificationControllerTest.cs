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

public class NotificationsControllerTest : BaseIntegrationTestFixture
{
    public NotificationsControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

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

    private async Task<IReadOnlyList<Notification>> AddSampleNotificationsToDatabase(
        User recipient,
        int count,
        bool read = false,
        NotificationType type = NotificationType.Follow,
        int? entityId = null,
        User? sender = null)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        var senderId = (sender ?? _sender).Id;
        var notifications = new List<Notification>();

        for (var i = 0; i < count; i++)
        {
            notifications.Add(new Notification
            {
                RecipientId = recipient.Id,
                SenderId = senderId,
                Type = type,
                EntityId = entityId,
                Read = read,
                CreatedAt = TestNow.AddMinutes(-i).UtcDateTime
            });
        }

        dbContext.Notification.AddRange(notifications);
        await dbContext.SaveChangesAsync();

        return notifications;
    }

    [Fact]
    public async Task List_NoNotificationsExist_EmptyListReturned()
    {
        var response = await HttpClient.GetAsync("/api/Notifications");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<NotificationPageDto>();
        result.ShouldNotBeNull();
        result.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task List_NotificationsExist_CorrectNumberReturned(int count)
    {
        var notificationsInDatabase = await AddSampleNotificationsToDatabase(_user, count);

        var response = await HttpClient.GetAsync("/api/Notifications");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<NotificationPageDto>();
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(count);

        var expectedIds = notificationsInDatabase.Select(n => n.Id).OrderBy(id => id);
        var actualIds = result.Items.Select(n => n.Id).OrderBy(id => id);
        actualIds.ShouldBe(expectedIds);
    }

    [Fact]
    public async Task List_ReturnsSenderName()
    {
        await AddSampleNotificationsToDatabase(_user, 1);

        var response = await HttpClient.GetAsync("/api/Notifications");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<NotificationPageDto>();
        result.ShouldNotBeNull();
        result.Items.Single().SenderId.ShouldBe(_sender.Id);
        result.Items.Single().SenderName.ShouldBe(_sender.DisplayName);
    }

    [Fact]
    public async Task List_UnreadOnlyFilterApplied_OnlyUnreadReturned()
    {
        await AddSampleNotificationsToDatabase(_user, 3, read: true);
        var unread = await AddSampleNotificationsToDatabase(_user, 2, read: false);

        var response = await HttpClient.GetAsync("/api/Notifications?unreadOnly=true");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<NotificationPageDto>();
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(unread.Count);
        result.Items.All(n => !n.Read).ShouldBeTrue();
    }

    [Fact]
    public async Task List_TypeFilterApplied_OnlyMatchingTypeReturned()
    {
        await AddSampleNotificationsToDatabase(_user, 2, type: NotificationType.Follow);
        var comments = await AddSampleNotificationsToDatabase(_user, 3, type: NotificationType.Comment);

        var response = await HttpClient.GetAsync("/api/Notifications?type=Comment");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<NotificationPageDto>();
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(comments.Count);
        result.Items.All(n => n.Type == NotificationType.Comment).ShouldBeTrue();
    }

    [Fact]
    public async Task List_PaginationApplied_ReturnsCorrectPageAndTotalPages()
    {
        await AddSampleNotificationsToDatabase(_user, 25);

        var response = await HttpClient.GetAsync("/api/Notifications?page=2&limit=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<NotificationPageDto>();
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(10);
        result.Page.ShouldBe(2);
        result.TotalPages.ShouldBe(3);
    }

    [Fact]
    public async Task UnreadCount_SomeUnreadExist_ReturnsCorrectCount()
    {
        await AddSampleNotificationsToDatabase(_user, 4, read: true);
        await AddSampleNotificationsToDatabase(_user, 3, read: false);

        var response = await HttpClient.GetAsync("/api/Notifications/unread-count");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        result.ShouldNotBeNull();
        result!["count"].ShouldBe(3);
    }

    [Fact]
    public async Task UnreadCount_NoneUnread_ReturnsZero()
    {
        var response = await HttpClient.GetAsync("/api/Notifications/unread-count");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        result.ShouldNotBeNull();
        result!["count"].ShouldBe(0);
    }

    [Fact]
    public async Task MarkRead_ValidRequest_ReturnsNoContentAndMarksRead()
    {
        var notifications = await AddSampleNotificationsToDatabase(_user, 1, read: false);

        var response = await HttpClient.PatchAsync($"/api/Notifications/{notifications[0].Id}/read", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var db = await DbContextFactory.CreateDbContextAsync();
        var updated = await db.Notification.FindAsync(notifications[0].Id);
        updated!.Read.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkRead_NotificationDoesNotExist_ReturnsNotFound()
    {
        var response = await HttpClient.PatchAsync("/api/Notifications/999999/read", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_BelongsToAnotherUser_ReturnsForbidden()
    {
        var otherUser = await CreateUserAsync("other@test.com", "Other User");
        var notifications = await AddSampleNotificationsToDatabase(otherUser, 1, read: false);

        var response = await HttpClient.PatchAsync($"/api/Notifications/{notifications[0].Id}/read", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkRead_Unauthenticated_ReturnsUnauthorized()
    {
        var notifications = await AddSampleNotificationsToDatabase(_user, 1, read: false);

        var response = await UnauthenticatedClient.PatchAsync($"/api/Notifications/{notifications[0].Id}/read", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAllRead_ValidRequest_ReturnsNoContentAndMarksAllRead()
    {
        await AddSampleNotificationsToDatabase(_user, 5, read: false);

        var response = await HttpClient.PatchAsync("/api/Notifications/read-all", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var db = await DbContextFactory.CreateDbContextAsync();
        var remainingUnread = await db.Notification.Where(n => n.RecipientId == _user.Id && !n.Read).CountAsync();
        remainingUnread.ShouldBe(0);
    }

    [Fact]
    public async Task MarkAllRead_DoesNotAffectOtherUsersNotifications()
    {
        var otherUser = await CreateUserAsync("other@test.com", "Other User");
        var otherUsersNotifications = await AddSampleNotificationsToDatabase(otherUser, 2, read: false);
        await AddSampleNotificationsToDatabase(_user, 2, read: false);

        var response = await HttpClient.PatchAsync("/api/Notifications/read-all", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var db = await DbContextFactory.CreateDbContextAsync();
        var stillUnread = await db.Notification
            .Where(n => n.RecipientId == otherUser.Id && !n.Read)
            .CountAsync();
        stillUnread.ShouldBe(otherUsersNotifications.Count);
    }

    [Fact]
    public async Task MarkAllRead_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.PatchAsync("/api/Notifications/read-all", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dismiss_ValidRequest_ReturnsNoContentAndDeletesNotification()
    {
        var notifications = await AddSampleNotificationsToDatabase(_user, 1);

        var response = await HttpClient.DeleteAsync($"/api/Notifications/{notifications[0].Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var db = await DbContextFactory.CreateDbContextAsync();
        var deleted = await db.Notification.FindAsync(notifications[0].Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task Dismiss_NotificationDoesNotExist_ReturnsNotFound()
    {
        var response = await HttpClient.DeleteAsync("/api/Notifications/999999");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dismiss_BelongsToAnotherUser_ReturnsForbidden()
    {
        var otherUser = await CreateUserAsync("other@test.com", "Other User");
        var notifications = await AddSampleNotificationsToDatabase(otherUser, 1);

        var response = await HttpClient.DeleteAsync($"/api/Notifications/{notifications[0].Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dismiss_Unauthenticated_ReturnsUnauthorized()
    {
        var notifications = await AddSampleNotificationsToDatabase(_user, 1);

        var response = await UnauthenticatedClient.DeleteAsync($"/api/Notifications/{notifications[0].Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
