using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;

namespace Grinder.Api.Tests.Integration.Hubs;

public class NotificationHubTests : BaseIntegrationTestFixture
{
    private CookieContainer _authCookies = null!;

    public NotificationHubTests(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true)
    {
    }

    private User _sender = null!;
    private User _recipient = null!;

    private sealed class CookieDelegatingHandler : DelegatingHandler
    {
        private readonly CookieContainer _cookies;

        public CookieDelegatingHandler(CookieContainer cookies, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _cookies = cookies;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cookieHeader = _cookies.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }
            return base.SendAsync(request, cancellationToken);
        }
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // recipient is the account we log HttpClient in as, since the hub
        // connection needs to authenticate as whoever should RECEIVE the push
        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        var recipient = new User
        {
            Email = "recipient@test.com",
            DisplayName = "Recipient",
            PasswordHash = "not-yet-hashed",
            Country = "NZ",
            IsVerified = true
        };
        recipient.PasswordHash = passwordHasher.HashPassword(recipient, "Password123!");
        dbContext.Users.Add(recipient);

        var sender = new User
        {
            Email = "sender@test.com",
            DisplayName = "Sender",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            IsVerified = true
        };
        dbContext.Users.Add(sender);

        await dbContext.SaveChangesAsync();

        _recipient = recipient;
        _sender = sender;

        var loginResponse = await HttpClient.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("recipient@test.com", "Password123!"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        _authCookies = BuildCookieContainerFromResponse(loginResponse, HttpClient.BaseAddress!);
    }

    private async Task<HubConnection> ConnectAsync()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"{HttpClient.BaseAddress}api/hubs/notifications", options =>
            {
                options.HttpMessageHandlerFactory = _ =>
                    new CookieDelegatingHandler(_authCookies, CreateServerHandler());
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    [Fact]
    public async Task CreateFollowNotification_PushesToRecipientOverHub()
    {
        await using var connection = await ConnectAsync();

        var tcs = new TaskCompletionSource<NotificationDto>();
        connection.On<NotificationDto>("ReceiveNotification", dto => tcs.TrySetResult(dto));

        var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.CreateAsync(_recipient.Id, _sender.Id, NotificationType.Follow);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.SenderId.ShouldBe(_sender.Id);
        received.Type.ShouldBe(NotificationType.Follow);
        received.Read.ShouldBeFalse();
    }

    [Fact]
    public async Task RefollowAfterUnfollow_StillPushesWithReadFalse()
    {
        await using var connection = await ConnectAsync();
        var notificationService = ServiceProvider.GetRequiredService<INotificationService>();

        await notificationService.CreateAsync(_recipient.Id, _sender.Id, NotificationType.Follow);

        await using (var db = await DbContextFactory.CreateDbContextAsync())
        {
            var existing = await db.Notification.FirstAsync(n => n.RecipientId == _recipient.Id && n.SenderId == _sender.Id);
            existing.Read = true;
            await db.SaveChangesAsync();
        }

        var tcs = new TaskCompletionSource<NotificationDto>();
        connection.On<NotificationDto>("ReceiveNotification", dto => tcs.TrySetResult(dto));

        await notificationService.CreateAsync(_recipient.Id, _sender.Id, NotificationType.Follow);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Read.ShouldBeFalse();
    }

    [Fact]
    public async Task SelfNotification_NeverPushed()
    {
        await using var connection = await ConnectAsync();

        var receivedAny = false;
        connection.On<NotificationDto>("ReceiveNotification", _ => receivedAny = true);

        var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.CreateAsync(_recipient.Id, _recipient.Id, NotificationType.Follow);

        await Task.Delay(500);
        receivedAny.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateNotification_PushesCorrectUnreadCount()
    {
        // seed two pre-existing unread notifications directly in the DB
        await using (var db = await DbContextFactory.CreateDbContextAsync())
        {
            db.Notification.AddRange(
                new Notification { RecipientId = _recipient.Id, SenderId = _sender.Id, Type = NotificationType.Comment, Read = false, CreatedAt = DateTime.UtcNow },
                new Notification { RecipientId = _recipient.Id, SenderId = _sender.Id, Type = NotificationType.React, Read = false, CreatedAt = DateTime.UtcNow }
            );
            await db.SaveChangesAsync();
        }

        await using var connection = await ConnectAsync();
        var tcs = new TaskCompletionSource<int>();
        connection.On<int>("ReceiveUnreadCount", count => tcs.TrySetResult(count));

        var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.CreateAsync(_recipient.Id, _sender.Id, NotificationType.Follow);

        var count = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        count.ShouldBe(3);
    }

    [Fact]
    public async Task DismissUnreadNotification_PushesUpdatedUnreadCount()
    {
        Notification notification = null!;
        await using (var db = await DbContextFactory.CreateDbContextAsync())
        {
            notification = new Notification { RecipientId = _recipient.Id, SenderId = _sender.Id, Type = NotificationType.Comment, Read = false, CreatedAt = DateTime.UtcNow };
            db.Notification.Add(notification);
            await db.SaveChangesAsync();
        }

        await using var connection = await ConnectAsync();
        var tcs = new TaskCompletionSource<int>();
        connection.On<int>("ReceiveUnreadCount", count => tcs.TrySetResult(count));

        var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.DeleteAsync(_recipient.Id, notification.Id);

        var count = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        count.ShouldBe(0);
    }

    [Fact]
    public async Task DismissReadNotification_DoesNotPushUnreadCount()
    {
        Notification notification = null!;
        await using (var db = await DbContextFactory.CreateDbContextAsync())
        {
            notification = new Notification { RecipientId = _recipient.Id, SenderId = _sender.Id, Type = NotificationType.Comment, Read = true, CreatedAt = DateTime.UtcNow };
            db.Notification.Add(notification);
            await db.SaveChangesAsync();
        }

        await using var connection = await ConnectAsync();
        var receivedAny = false;
        connection.On<int>("ReceiveUnreadCount", _ => receivedAny = true);

        var notificationService = ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.DeleteAsync(_recipient.Id, notification.Id);

        await Task.Delay(500);
        receivedAny.ShouldBeFalse();
    }
}
