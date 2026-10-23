using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Models.DTOs;

namespace Grinder.Api.Tests.Integration.Services;

public class CommentServiceTest : BaseIntegrationTestFixture
{
    private int _userId;

    private ICommentService ServiceUnderTest =>
        ServiceProvider.GetRequiredService<ICommentService>();

    public CommentServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await AddUser("test@test.com", "Test User");
    }

    private async Task<int> AddUser(string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = string.Empty,
            Country = "NZ",
            IsVerified = true
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    private async Task<TaskItem> AddTask(
        int userId,
        string title = "Test Task",
        TaskVisibility visibility = TaskVisibility.Private)
    {
        var task = new TaskItem
        {
            UserId = userId,
            Title = title,
            DueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(7)),
            Visibility = visibility
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private async Task<Comment> AddComment(
        int taskId,
        int authorId,
        string content = "Test Comment",
        int? parentCommentId = null)
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

    [Fact]
    public async Task CreateCommentAsync_ValidRequest_CommentCreated()
    {
        var task = await AddTask(_userId);
        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "This is a test comment"
        };

        var result = await ServiceUnderTest.CreateCommentAsync(_userId, request);

        result.ShouldNotBeNull();
        result.Content.ShouldBe("This is a test comment");
        result.TaskId.ShouldBe(task.Id);
        result.AuthorId.ShouldBe(_userId);
    }

    [Fact]
    public async Task CreateCommentAsync_WithParentComment_ReplyCreated()
    {
        var task = await AddTask(_userId);
        var parentComment = await AddComment(task.Id, _userId, "Parent comment");

        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "This is a reply",
            ParentCommentId = parentComment.Id
        };

        var result = await ServiceUnderTest.CreateCommentAsync(_userId, request);

        result.ShouldNotBeNull();
        result.ParentCommentId.ShouldBe(parentComment.Id);
    }

    [Fact]
    public async Task CreateCommentAsync_TaskNotFound_ThrowsKeyNotFoundException()
    {
        var request = new CreateCommentRequest
        {
            TaskId = 99999,
            Content = "Comment on non-existent task"
        };

        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await ServiceUnderTest.CreateCommentAsync(_userId, request));
    }

    [Fact]
    public async Task CreateCommentAsync_PrivateTaskOwnedByAnotherUser_ThrowsUnauthorizedAccessException()
    {
        var otherUserId = await AddUser("other@test.com", "Other User");
        var task = await AddTask(otherUserId, visibility: TaskVisibility.Private);

        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Unauthorized comment"
        };

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await ServiceUnderTest.CreateCommentAsync(_userId, request));
    }

    [Fact]
    public async Task CreateCommentAsync_FriendsOnlyTaskWithOneWayFollow_ThrowsUnauthorizedAccessException()
    {
        var ownerId = await AddUser("owner@test.com", "Owner");
        var task = await AddTask(ownerId, visibility: TaskVisibility.FriendsOnly);
        await AddFollow(_userId, ownerId);

        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Unauthorized comment"
        };

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await ServiceUnderTest.CreateCommentAsync(_userId, request));
    }

    [Fact]
    public async Task CreateCommentAsync_FriendsOnlyTaskWithMutualFollow_CreatesComment()
    {
        var ownerId = await AddUser("owner@test.com", "Owner");
        var task = await AddTask(ownerId, visibility: TaskVisibility.FriendsOnly);
        await AddFollow(_userId, ownerId);
        await AddFollow(ownerId, _userId);

        var result = await ServiceUnderTest.CreateCommentAsync(_userId, new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Friend comment"
        });

        result.Content.ShouldBe("Friend comment");
    }

    [Fact]
    public async Task DeleteCommentAsync_LeafComment_HardDeleted()
    {
        var task = await AddTask(_userId);
        var comment = await AddComment(task.Id, _userId);

        var result = await ServiceUnderTest.DeleteCommentAsync(_userId, comment.Id);

        result.Removed.ShouldBeTrue();
        result.Comment.ShouldBeNull();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.Comments.FindAsync(comment.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteCommentAsync_WithNotificationReference_DeletesSuccessfully()
    {
        var task = await AddTask(_userId);
        var comment = await AddComment(task.Id, _userId);

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Notification.Add(new Notification
            {
                RecipientId = _userId,
                SenderId = _userId,
                Type = NotificationType.Comment,
                EntityId = task.Id,
                CommentId = comment.Id,
                CreatedAt = DateTime.UtcNow,
                Read = false,
            });
            await context.SaveChangesAsync();
        }

        var result = await ServiceUnderTest.DeleteCommentAsync(_userId, comment.Id);

        result.Removed.ShouldBeTrue();
        result.Comment.ShouldBeNull();

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            (await context.Comments.FindAsync(comment.Id)).ShouldBeNull();
            (await context.Notification.FirstOrDefaultAsync(n => n.CommentId == comment.Id)).ShouldBeNull();
        }
    }

    [Fact]
    public async Task DeleteCommentAsync_TaskOwnerDeletingOthersLeafComment_HardDeleted()
    {
        var otherUserId = await AddUser("other@test.com", "Other User");
        var task = await AddTask(_userId);
        var comment = await AddComment(task.Id, otherUserId);

        var result = await ServiceUnderTest.DeleteCommentAsync(_userId, comment.Id);

        result.Removed.ShouldBeTrue();
        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.Comments.FindAsync(comment.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteCommentAsync_NotAuthorOrTaskOwner_ThrowsUnauthorizedAccessException()
    {
        var otherUserId = await AddUser("other@test.com", "Other User");
        var task = await AddTask(otherUserId);
        var comment = await AddComment(task.Id, otherUserId);

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await ServiceUnderTest.DeleteCommentAsync(_userId, comment.Id));
    }

    [Fact]
    public async Task DeleteCommentAsync_ParentComment_PreservesRepliesAndReturnsDeletedPlaceholder()
    {
        var task = await AddTask(_userId);
        var parent = await AddComment(task.Id, _userId, "Parent comment");
        var reply = await AddComment(task.Id, _userId, "Reply", parent.Id);

        await ServiceUnderTest.DeleteCommentAsync(_userId, parent.Id);

        var result = await ServiceUnderTest.GetCommentsByTaskIdAsync(_userId, task.Id);

        result.Comments.Count.ShouldBe(1);
        result.Comments[0].IsDeleted.ShouldBeTrue();
        result.Comments[0].Content.ShouldBe("Deleted comment");
        result.Comments[0].ReplyCount.ShouldBe(1);
        // Soft-deleted comments are excluded from the task total count.
        result.TotalCount.ShouldBe(1);

        var repliesResponse = await ServiceUnderTest.GetRepliesByCommentIdAsync(_userId, parent.Id);
        repliesResponse.Replies[0].Id.ShouldBe(reply.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var tombstoned = await context.Comments.FindAsync(parent.Id);
        tombstoned.ShouldNotBeNull();
        tombstoned.Content.ShouldBeEmpty();
        (await context.Comments.FindAsync(reply.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task GetCommentsByTaskIdAsync_DeletedComments_AreExcludedFromTotalCount()
    {
        var task = await AddTask(_userId);
        var active = await AddComment(task.Id, _userId, "Active");
        var deleted = await AddComment(task.Id, _userId, "Deleted");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var dbComment = await context.Comments.FindAsync(deleted.Id);
        dbComment.IsDeleted = true;
        await context.SaveChangesAsync();

        var result = await ServiceUnderTest.GetCommentsByTaskIdAsync(_userId, task.Id);

        result.Comments.Count.ShouldBe(2);
        result.Comments.ShouldContain(c => c.Id == active.Id);
        result.Comments.ShouldContain(c => c.Id == deleted.Id && c.IsDeleted);
        result.TotalCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task GetCommentsByTaskIdAsync_MultipleComments_AllReturned(int commentCount)
    {
        var task = await AddTask(_userId);
        for (int i = 0; i < commentCount; i++)
        {
            await AddComment(task.Id, _userId, $"Comment {i}");
        }

        var result = await ServiceUnderTest.GetCommentsByTaskIdAsync(_userId, task.Id);

        result.Comments.Count.ShouldBe(commentCount);
        result.TotalCount.ShouldBe(commentCount);
    }

    [Fact]
    public async Task GetCommentsByTaskIdAsync_WithReplies_HierarchicalStructureReturned()
    {
        var task = await AddTask(_userId);
        var parentComment = await AddComment(task.Id, _userId, "Parent comment");
        await AddComment(task.Id, _userId, "Reply 1", parentComment.Id);
        await AddComment(task.Id, _userId, "Reply 2", parentComment.Id);

        var result = await ServiceUnderTest.GetCommentsByTaskIdAsync(_userId, task.Id);

        result.Comments.Count.ShouldBe(1);
        result.Comments[0].ReplyCount.ShouldBe(2);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetCommentsByTaskIdAsync_TaskNotFound_ThrowsKeyNotFoundException()
    {
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await ServiceUnderTest.GetCommentsByTaskIdAsync(_userId, 99999));
    }

    [Fact]
    public async Task GetCommentsByTaskIdAsync_PrivateTaskOwnedByAnotherUser_ThrowsUnauthorizedAccessException()
    {
        var otherUserId = await AddUser("other@test.com", "Other User");
        var task = await AddTask(otherUserId, visibility: TaskVisibility.Private);

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await ServiceUnderTest.GetCommentsByTaskIdAsync(_userId, task.Id));
    }
}