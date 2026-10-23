using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class AttachmentUploadsControllerTest : BaseIntegrationTestFixture
{
    public AttachmentUploadsControllerTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory, handleCookies: true) { }

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

        var loginRequest = new { Email = "test@test.com", Password = "Password123!" };
        await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    private async Task<TaskAttachment> CreateAttachmentInDb(int userId, int? taskItemId = null)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var attachment = new TaskAttachment
        {
            UserId = userId,
            TaskItemId = taskItemId,
            StoredFileName = $"{Guid.NewGuid():N}.png",
            ContentType = "image/png",
            FileSize = 1024,
            Url = "/uploads/taskAttachments/test.png",
            CreatedAt = TestNow.UtcDateTime
        };

        context.TaskAttachments.Add(attachment);
        await context.SaveChangesAsync();

        return attachment;
    }

    private async Task<User> CreateOtherUser()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var user = new User
        {
            Email = "other@test.com",
            DisplayName = "Other User",
            PasswordHash = "NotARealHash",
            Country = "NZ",
            IsVerified = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private async Task<int> CreateTaskItemForUser(int userId)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        User user = await CreateOtherUser();
        var taskItem = new TaskItem
        {
            UserId = user.Id,
            Title = "Test Task",
            ItemStatus = TaskItemStatus.Todo,
            DueDate = DateOnly.FromDateTime(TestNow.AddDays(2).DateTime),
            CreatedAt = TestNow
        };

        context.TaskItems.Add(taskItem);
        await context.SaveChangesAsync();

        return taskItem.Id;
    }

    [Fact]
    public async Task Delete_ValidRequest_OwnTemporaryAttachment_ReturnsNoContent()
    {
        TaskAttachment attachment = await CreateAttachmentInDb(_user.Id);

        var response = await HttpClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskAttachments.AnyAsync(a => a.Id == attachment.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_ValidRequest_OwnLinkedAttachment_ReturnsNoContent()
    {
        int taskItemId = await CreateTaskItemForUser(_user.Id);
        TaskAttachment attachment = await CreateAttachmentInDb(_user.Id, taskItemId);

        var response = await HttpClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskAttachments.AnyAsync(a => a.Id == attachment.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_AttachmentDoesNotExist_ReturnsNotFound()
    {
        var response = await HttpClient.DeleteAsync("/api/AttachmentUploads/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AttachmentBelongsToDifferentUser_ReturnsNotFound()
    {
        User otherUser = await CreateOtherUser();
        TaskAttachment attachment = await CreateAttachmentInDb(otherUser.Id);

        var response = await HttpClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Verify the attachment was NOT deleted
        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskAttachments.AnyAsync(a => a.Id == attachment.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_NotLoggedIn_ReturnsUnauthorized()
    {
        TaskAttachment attachment = await CreateAttachmentInDb(_user.Id);

        var response = await UnauthenticatedClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Verify the attachment was NOT deleted
        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskAttachments.AnyAsync(a => a.Id == attachment.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Upload_ValidRequest_ReturnsOkWithAttachment()
    {
        using var content = new MultipartFormDataContent();
        byte[] fileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "photo.png");

        var response = await HttpClient.PostAsync("/api/AttachmentUploads", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var attachment = await response.Content.ReadFromJsonAsync<TaskAttachment>();
        attachment.ShouldNotBeNull();
        attachment.TaskItemId.ShouldBeNull();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        TaskAttachment? saved = await context.TaskAttachments.FirstOrDefaultAsync(a => a.Id == attachment.Id);
        saved.ShouldNotBeNull();
        saved.UserId.ShouldBe(_user.Id);
    }

    [Fact]
    public async Task Upload_NoFileProvided_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent(); // intentionally empty

        var response = await HttpClient.PostAsync("/api/AttachmentUploads", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "empty.png");

        var response = await HttpClient.PostAsync("/api/AttachmentUploads", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_UnsupportedFileType_ReturnsBadRequestWithErrorCode()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x00, 0x01, 0x02]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-msdownload");
        content.Add(fileContent, "file", "malware.exe");

        var response = await HttpClient.PostAsync("/api/AttachmentUploads", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.ShouldContainKey("code");
        body.ShouldContainKey("message");
    }

    [Fact]
    public async Task Upload_NotLoggedIn_ReturnsUnauthorized()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "photo.png");

        var response = await UnauthenticatedClient.PostAsync("/api/AttachmentUploads", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_LinkedAttachment_DoesNotDeleteParentTask()
    {
        int taskItemId = await CreateTaskItemForUser(_user.Id);
        TaskAttachment attachment = await CreateAttachmentInDb(_user.Id, taskItemId);

        var response = await HttpClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        (await context.TaskItems.AnyAsync(t => t.Id == taskItemId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_SameAttachmentTwice_SecondRequestReturnsNotFound()
    {
        TaskAttachment attachment = await CreateAttachmentInDb(_user.Id);

        var firstResponse = await HttpClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");
        var secondResponse = await HttpClient.DeleteAsync($"/api/AttachmentUploads/{attachment.Id}");

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
