using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Grinder.Api.Tests.Integration.Controllers;

public class CommentControllerTest : BaseIntegrationTestFixture
{
    private int _userId;

    public CommentControllerTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory, handleCookies: true) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();
    }

    private async Task RegisterAndLogin()
    {
        var user = CreateUser("test@test.com", "Test User");
        user.PasswordHash = ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, "Password123!");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        _userId = user.Id;

        var login = await HttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test@test.com", "Password123!"));

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static User CreateUser(string email, string displayName) => new()
    {
        Email = email,
        DisplayName = displayName,
        PasswordHash = string.Empty,
        Country = "NZ",
        IsVerified = true
    };

    private async Task<int> AddOtherUser(string email = "other@test.com", string displayName = "Other User")
    {
        var user = CreateUser(email, displayName);
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

    [Fact]
    public async Task CreateComment_ValidRequest_CommentCreated()
    {
        var task = await AddTask(_userId);
        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "This is a test comment"
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        comment.ShouldNotBeNull();
        comment.Content.ShouldBe("This is a test comment");
        comment.TaskId.ShouldBe(task.Id);
        comment.AuthorId.ShouldBe(_userId);
    }

    [Fact]
    public async Task CreateComment_WithParentComment_ReplyCreated()
    {
        var task = await AddTask(_userId);
        var parentComment = await AddComment(task.Id, _userId, "Parent comment");

        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "This is a reply",
            ParentCommentId = parentComment.Id
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        comment.ShouldNotBeNull();
        comment.ParentCommentId.ShouldBe(parentComment.Id);
    }

    [Fact]
    public async Task CreateComment_TaskNotFound_ReturnsNotFound()
    {
        var request = new CreateCommentRequest
        {
            TaskId = 99999,
            Content = "Comment on non-existent task"
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateComment_PrivateTaskOwnedByAnotherUser_ReturnsForbidden()
    {
        var otherUserId = await AddOtherUser();
        var task = await AddTask(otherUserId, visibility: TaskVisibility.Private);

        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Unauthorized comment"
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateComment_Unauthenticated_ReturnsUnauthorized()
    {
        var task = await AddTask(_userId);
        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = "Unauthenticated comment"
        };

        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteComment_LeafComment_HardDeleted()
    {
        var task = await AddTask(_userId);
        var comment = await AddComment(task.Id, _userId);

        var response = await HttpClient.DeleteAsync($"/api/Comment/{comment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DeleteCommentResult>();
        result.ShouldNotBeNull();
        result.Removed.ShouldBeTrue();
        result.Comment.ShouldBeNull();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.Comments.FindAsync(comment.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteComment_CommentWithReplies_Tombstoned()
    {
        var task = await AddTask(_userId);
        var parent = await AddComment(task.Id, _userId, "Parent comment");
        await AddComment(task.Id, _userId, "Reply", parent.Id);

        var response = await HttpClient.DeleteAsync($"/api/Comment/{parent.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DeleteCommentResult>();
        result.ShouldNotBeNull();
        result.Removed.ShouldBeFalse();
        result.Comment.ShouldNotBeNull();
        result.Comment.IsDeleted.ShouldBeTrue();
        result.Comment.Content.ShouldBe("Deleted comment");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var stillThere = await context.Comments.FindAsync(parent.Id);
        stillThere.ShouldNotBeNull();
        stillThere.IsDeleted.ShouldBeTrue();
        stillThere.Content.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteComment_TaskOwnerDeletingOthersLeafComment_HardDeleted()
    {
        var otherUserId = await AddOtherUser();
        var task = await AddTask(_userId);
        var comment = await AddComment(task.Id, otherUserId);

        var response = await HttpClient.DeleteAsync($"/api/Comment/{comment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.Comments.FindAsync(comment.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteComment_NotAuthorOrTaskOwner_ReturnsForbidden()
    {
        var otherUserId = await AddOtherUser();
        var task = await AddTask(otherUserId);
        var comment = await AddComment(task.Id, otherUserId);

        var response = await HttpClient.DeleteAsync($"/api/Comment/{comment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteComment_Unauthenticated_ReturnsUnauthorized()
    {
        var task = await AddTask(_userId);
        var comment = await AddComment(task.Id, _userId);

        var response = await UnauthenticatedClient.DeleteAsync($"/api/Comment/{comment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCommentsByTaskId_MultipleComments_AllReturned()
    {
        var task = await AddTask(_userId);
        await AddComment(task.Id, _userId, "First comment");
        await AddComment(task.Id, _userId, "Second comment");
        await AddComment(task.Id, _userId, "Third comment");

        var response = await HttpClient.GetAsync($"/api/Comment/task/{task.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TaskCommentsResponse>();
        result.ShouldNotBeNull();
        result.Comments.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetCommentsByTaskId_WithReplies_HierarchicalStructureReturned()
    {
        var task = await AddTask(_userId);
        var parentComment = await AddComment(task.Id, _userId, "Parent comment");
        await AddComment(task.Id, _userId, "Reply 1", parentComment.Id);
        await AddComment(task.Id, _userId, "Reply 2", parentComment.Id);

        var response = await HttpClient.GetAsync($"/api/Comment/task/{task.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TaskCommentsResponse>();
        result.ShouldNotBeNull();
        result.Comments.Count.ShouldBe(1);
        result.Comments[0].ReplyCount.ShouldBe(2);
    
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetCommentsByTaskId_TaskNotFound_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync("/api/Comment/task/99999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCommentsByTaskId_PrivateTaskOwnedByAnotherUser_ReturnsForbidden()
    {
        var otherUserId = await AddOtherUser();
        var task = await AddTask(otherUserId, visibility: TaskVisibility.Private);

        var response = await HttpClient.GetAsync($"/api/Comment/task/{task.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCommentsByTaskId_Unauthenticated_ReturnsUnauthorized()
    {
        var task = await AddTask(_userId);

        var response = await UnauthenticatedClient.GetAsync($"/api/Comment/task/{task.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateComment_InvalidRequestCommentTooLong_ReturnsBadRequestCommentNotCreated()
    {
        var task = await AddTask(_userId);
        var longContent = new string('a', 2048 + 1); // Max length is 2048 characters, so this should trigger a validation error
        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = longContent
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var commentCount = context.Comments.Count(c => c.TaskId == task.Id);
        commentCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateComment_InvalidRequestEmptyComment_ReturnsBadRequestCommentNotCreated()
    {
        var task = await AddTask(_userId);
        var request = new CreateCommentRequest
        {
            TaskId = task.Id,
            Content = ""
        };

        var response = await HttpClient.PostAsJsonAsync("/api/Comment", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var commentCount = context.Comments.Count(c => c.TaskId == task.Id);
        commentCount.ShouldBe(0);
    }

}