using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class NotificationServiceTest : BaseIntegrationTestFixture
{
    private INotificationService ServiceUnderTest => ServiceProvider.GetRequiredService<INotificationService>();

    public NotificationServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUserToDatabaseAsync(string email, string displayName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = FakeTimeProvider.GetUtcNow().UtcDateTime
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private async Task<Notification> AddNotificationToDatabaseAsync(
        int recipientId,
        int senderId,
        NotificationType type,
        bool read = false,
        DateTime? createdAt = null,
        int? entityId = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var notification = new Notification
        {
            RecipientId = recipientId,
            SenderId = senderId,
            Type = type,
            EntityId = entityId,
            Read = read,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        context.Notification.Add(notification);
        await context.SaveChangesAsync();

        return notification;
    }

    [Fact]
    public async Task CreateAsync_RecipientAndSenderSame_DoesNotCreateNotification()
    {
        var user = await AddUserToDatabaseAsync("user@example.com", "user");

        await ServiceUnderTest.CreateAsync(user.Id, user.Id, NotificationType.Comment);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Notification.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_FollowNotificationAlreadyExists_UpdatesExistingNotification()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        var oldCreatedAt = DateTime.UtcNow.AddDays(-2);
        await AddNotificationToDatabaseAsync(
            recipient.Id,
            sender.Id,
            NotificationType.Follow,
            read: true,
            createdAt: oldCreatedAt);

        await ServiceUnderTest.CreateAsync(recipient.Id, sender.Id, NotificationType.Follow);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Notification.Count().ShouldBe(1);

        var notification = context.Notification.ShouldHaveSingleItem();
        notification.Read.ShouldBeFalse();
        notification.CreatedAt.ShouldBeGreaterThan(oldCreatedAt);
    }

    [Fact]
    public async Task CreateAsync_Message_CreatesNotification()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        await ServiceUnderTest.CreateAsync(recipient.Id, sender.Id, NotificationType.Message);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var notification = context.Notification.ShouldHaveSingleItem();
        notification.RecipientId.ShouldBe(recipient.Id);
        notification.SenderId.ShouldBe(sender.Id);
        notification.Type.ShouldBe(NotificationType.Message);
        notification.Read.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_UnreadMessageNotificationAlreadyExists_BumpsExistingInsteadOfDuplicating()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        var oldCreatedAt = DateTime.UtcNow.AddMinutes(-5);
        var existing = await AddNotificationToDatabaseAsync(
            recipient.Id,
            sender.Id,
            NotificationType.Message,
            read: false,
            createdAt: oldCreatedAt);

        await ServiceUnderTest.CreateAsync(recipient.Id, sender.Id, NotificationType.Message);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Notification.Count().ShouldBe(1);

        var notification = context.Notification.ShouldHaveSingleItem();
        notification.Id.ShouldBe(existing.Id);
        notification.CreatedAt.ShouldBeGreaterThan(oldCreatedAt);
    }

    [Fact]
    public async Task CreateAsync_ReadMessageNotificationAlreadyExists_CreatesNewNotification()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Message, read: true);

        await ServiceUnderTest.CreateAsync(recipient.Id, sender.Id, NotificationType.Message);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Notification.Count().ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_FilterByTypeAndUnreadOnly_ReturnsMatchingNotifications()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var otherRecipient = await AddUserToDatabaseAsync("other-recipient@example.com", "other recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Follow, read: false, createdAt: DateTime.UtcNow.AddMinutes(-1));
        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Comment, read: false, createdAt: DateTime.UtcNow.AddMinutes(-2));
        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Follow, read: true, createdAt: DateTime.UtcNow.AddMinutes(-3));
        await AddNotificationToDatabaseAsync(otherRecipient.Id, sender.Id, NotificationType.Follow, read: false, createdAt: DateTime.UtcNow);

        var result = await ServiceUnderTest.ListAsync(recipient.Id, 1, 20, NotificationType.Follow, unreadOnly: true);

        result.Items.Count.ShouldBe(1);
        var returned = result.Items.ShouldHaveSingleItem();
        returned.Type.ShouldBe(NotificationType.Follow);
        returned.Read.ShouldBeFalse();
        returned.SenderName.ShouldBe("sender");
    }

    [Fact]
    public async Task ListAsync_PageAndLimitOutsideBounds_AreClamped()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        var baseTime = DateTime.UtcNow.AddHours(-1);

        for (var i = 0; i < 101; i++)
        {
            await AddNotificationToDatabaseAsync(
                recipient.Id,
                sender.Id,
                NotificationType.Comment,
                read: false,
                createdAt: baseTime.AddSeconds(i),
                entityId: i);
        }

        var result = await ServiceUnderTest.ListAsync(recipient.Id, page: 0, limit: 1000, type: null, unreadOnly: false);

        result.Page.ShouldBe(1);
        result.TotalPages.ShouldBe(2);
        result.Items.Count.ShouldBe(100);
        result.Items.First().EntityId.ShouldBe(100);
    }

    [Fact]
    public async Task UnreadCountAsync_CountsOnlyUnreadNotificationsForRecipient()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var otherRecipient = await AddUserToDatabaseAsync("other-recipient@example.com", "other recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Comment, read: false);
        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Follow, read: false);
        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Follow, read: true);
        await AddNotificationToDatabaseAsync(otherRecipient.Id, sender.Id, NotificationType.Comment, read: false);

        var unreadCount = await ServiceUnderTest.UnreadCountAsync(recipient.Id);

        unreadCount.ShouldBe(2);
    }

    [Fact]
    public async Task MarkReadAsync_OwnedUnreadNotification_SetsReadToTrue()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        var notification = await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Comment, read: false);

        await ServiceUnderTest.MarkReadAsync(recipient.Id, notification.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var updatedNotification = context.Notification.ShouldHaveSingleItem();
        updatedNotification.Read.ShouldBeTrue();
    }

    [Fact]
    public async Task MarkReadAsync_NotificationOwnedByDifferentUser_ThrowsUnauthorizedAccessException()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var otherUser = await AddUserToDatabaseAsync("other-user@example.com", "other user");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        var notification = await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Comment, read: false);

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await ServiceUnderTest.MarkReadAsync(otherUser.Id, notification.Id));
    }

    [Fact]
    public async Task MarkReadAsync_NoNotificationId_MarksAllNotificationsRead()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var otherRecipient = await AddUserToDatabaseAsync("other-recipient@example.com", "other recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Comment, read: false);
        await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Follow, read: true);
        await AddNotificationToDatabaseAsync(otherRecipient.Id, sender.Id, NotificationType.Comment, read: false);

        await ServiceUnderTest.MarkReadAsync(recipient.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var recipientNotifications = context.Notification
            .Where(n => n.RecipientId == recipient.Id)
            .ToList();
        var otherRecipientNotification = context.Notification
            .Where(n => n.RecipientId == otherRecipient.Id)
            .ShouldHaveSingleItem();

        recipientNotifications.All(n => n.Read).ShouldBeTrue();
        otherRecipientNotification.Read.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_OwnedNotification_RemovesNotification()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");
        var sender = await AddUserToDatabaseAsync("sender@example.com", "sender");

        var notification = await AddNotificationToDatabaseAsync(recipient.Id, sender.Id, NotificationType.Comment, read: false);

        await ServiceUnderTest.DeleteAsync(recipient.Id, notification.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Notification.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NotificationDoesNotExist_ThrowsKeyNotFoundException()
    {
        var recipient = await AddUserToDatabaseAsync("recipient@example.com", "recipient");

        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await ServiceUnderTest.DeleteAsync(recipient.Id, 999999));
    }
}
