using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;

namespace Grinder.Api.Tests.Integration.Services;

public class CommentNotificationTest : BaseIntegrationTestFixture
{
    private ICommentService CommentService =>
        ServiceProvider.GetRequiredService<ICommentService>();

    public CommentNotificationTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUser(string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            IsVerified = true
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<TaskItem> AddTask(int userId, TaskVisibility visibility = TaskVisibility.Public)
    {
        var task = new TaskItem
        {
            UserId = userId,
            Title = "Test Task",
            DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7)),
            Visibility = visibility
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private async Task<Comment> AddComment(int taskId, int authorId, string content = "Test Comment", int? parentCommentId = null)
    {
        var comment = new Comment
        {
            TaskId = taskId,
            AuthorId = authorId,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
            ParentCommentId = parentCommentId
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
        return comment;
    }

    private async Task AddFollow(int followerId, int followedId)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.Add(new Follow { FollowerId = followerId, FollowedId = followedId });
        await context.SaveChangesAsync();
    }

    private async Task<List<Notification>> GetNotificationsForUser(int userId)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        return await context.Notification
            .Where(n => n.RecipientId == userId)
            .ToListAsync();
    }

    [Fact]
    public async Task CreateComment_OnAnotherUsersTask_NotifiesTaskOwner()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var commenter = await AddUser("commenter@test.com", "Commenter");
        var task = await AddTask(taskOwner.Id);

        await CommentService.CreateCommentAsync(commenter.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Great task!"
        });

        var notifications = await GetNotificationsForUser(taskOwner.Id);
        notifications.Count.ShouldBe(1);
        notifications[0].SenderId.ShouldBe(commenter.Id);
        notifications[0].Type.ShouldBe(NotificationType.Comment);
        notifications[0].EntityId.ShouldBe(task.Id);
        notifications[0].Read.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateComment_OnOwnTask_DoesNotNotifySelf()
    {
        var user = await AddUser("user@test.com", "User");
        var task = await AddTask(user.Id);

        await CommentService.CreateCommentAsync(user.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Commenting on my own task"
        });

        var notifications = await GetNotificationsForUser(user.Id);
        notifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateReply_ToAnotherUsersComment_NotifiesParentCommentAuthor()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var originalCommenter = await AddUser("commenter@test.com", "Commenter");
        var replier = await AddUser("replier@test.com", "Replier");
        var task = await AddTask(taskOwner.Id);
        var parentComment = await AddComment(task.Id, originalCommenter.Id, "Original comment");

        await CommentService.CreateCommentAsync(replier.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "This is a reply",
            ParentCommentId = parentComment.Id
        });

        var notifications = await GetNotificationsForUser(originalCommenter.Id);
        notifications.Count.ShouldBe(1);
        notifications[0].SenderId.ShouldBe(replier.Id);
        notifications[0].Type.ShouldBe(NotificationType.Comment);
        notifications[0].EntityId.ShouldBe(task.Id);
    }

    [Fact]
    public async Task CreateReply_ToOwnComment_DoesNotNotifySelf()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var commenter = await AddUser("commenter@test.com", "Commenter");
        var task = await AddTask(taskOwner.Id);
        var parentComment = await AddComment(task.Id, commenter.Id, "My comment");

        await CommentService.CreateCommentAsync(commenter.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Replying to myself",
            ParentCommentId = parentComment.Id
        });

        var notifications = await GetNotificationsForUser(commenter.Id);
        notifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateReply_NotifiesParentCommentAuthor_NotTaskOwner()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var originalCommenter = await AddUser("commenter@test.com", "Commenter");
        var replier = await AddUser("replier@test.com", "Replier");
        var task = await AddTask(taskOwner.Id);
        var parentComment = await AddComment(task.Id, originalCommenter.Id, "Original comment");

        await CommentService.CreateCommentAsync(replier.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "This is a reply",
            ParentCommentId = parentComment.Id
        });

        // The parent comment author should be notified
        var commenterNotifications = await GetNotificationsForUser(originalCommenter.Id);
        commenterNotifications.Count.ShouldBe(1);

        // The task owner should NOT get a notification for a reply (only for top-level comments)
        var ownerNotifications = await GetNotificationsForUser(taskOwner.Id);
        ownerNotifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultipleComments_OnSameTask_CreatesMultipleNotifications()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var commenter1 = await AddUser("commenter1@test.com", "Commenter 1");
        var commenter2 = await AddUser("commenter2@test.com", "Commenter 2");
        var task = await AddTask(taskOwner.Id);

        await CommentService.CreateCommentAsync(commenter1.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "First comment"
        });

        await CommentService.CreateCommentAsync(commenter2.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Second comment"
        });

        var notifications = await GetNotificationsForUser(taskOwner.Id);
        notifications.Count.ShouldBe(2);
        notifications.ShouldAllBe(n => n.Type == NotificationType.Comment);
        notifications.Select(n => n.SenderId).ShouldContain(commenter1.Id);
        notifications.Select(n => n.SenderId).ShouldContain(commenter2.Id);
    }

    [Fact]
    public async Task CreateComment_NotificationEntityId_IsTheTaskId()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var commenter = await AddUser("commenter@test.com", "Commenter");
        var task = await AddTask(taskOwner.Id);

        await CommentService.CreateCommentAsync(commenter.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "A comment"
        });

        var notifications = await GetNotificationsForUser(taskOwner.Id);
        notifications.ShouldHaveSingleItem().EntityId.ShouldBe(task.Id);
    }

    [Fact]
    public async Task CreateReply_OnFriendsOnlyTask_NotifiesParentCommentAuthor()
    {
        var taskOwner = await AddUser("owner@test.com", "Task Owner");
        var commenter = await AddUser("commenter@test.com", "Commenter");
        var replier = await AddUser("replier@test.com", "Replier");

        // Make commenter and task owner friends
        await AddFollow(commenter.Id, taskOwner.Id);
        await AddFollow(taskOwner.Id, commenter.Id);
        // Make replier and task owner friends
        await AddFollow(replier.Id, taskOwner.Id);
        await AddFollow(taskOwner.Id, replier.Id);

        var task = await AddTask(taskOwner.Id, TaskVisibility.FriendsOnly);
        var parentComment = await AddComment(task.Id, commenter.Id, "Friend comment");

        await CommentService.CreateCommentAsync(replier.Id, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Reply from another friend",
            ParentCommentId = parentComment.Id
        });

        var notifications = await GetNotificationsForUser(commenter.Id);
        notifications.Count.ShouldBe(1);
        notifications[0].SenderId.ShouldBe(replier.Id);
        notifications[0].Type.ShouldBe(NotificationType.Comment);
    }
}
