using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class MediaControllerTest : BaseIntegrationTestFixture
{
    public MediaControllerTest(WebApplicationFactory<Program> webAppFactory)
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

    /// <summary>
    /// Direct DB insert, no file on disk — matches CreateAttachmentInDb in
    /// AttachmentUploadsControllerTest. Only safe to use for cases where the
    /// controller/service returns before ever touching the physical file
    /// (not found, wrong owner, not authenticated).
    /// </summary>
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

    /// <summary>
    /// Creates a real attachment (real file on disk, wherever
    /// IAttachmentService actually stores it) by going through the same
    /// upload endpoint AttachmentUploadsControllerTest uses. Needed for any
    /// GetTaskAttachment test that expects a 200 with real bytes back.
    /// </summary>
    private async Task<TaskAttachment> UploadAttachmentAsCurrentUser(string fileName = "photo.png")
    {
        using var content = new MultipartFormDataContent();
        byte[] fileBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", fileName);

        var response = await HttpClient.PostAsync("/api/AttachmentUploads", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var attachment = await response.Content.ReadFromJsonAsync<TaskAttachment>();
        attachment.ShouldNotBeNull();
        return attachment;
    }

    private async Task UploadProfilePictureAsCurrentUser()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "avatar.png");

        var response = await HttpClient.PutAsync("/api/user/profile/upload", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProfilePicture_UserHasProfilePicture_ReturnsOkWithFile()
    {
        await UploadProfilePictureAsCurrentUser();

        var response = await HttpClient.GetAsync($"/api/media/{_user.Id}/profile-picture");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        response.Headers.ETag.ShouldNotBeNull();
    }


    [Fact]
    public async Task GetProfilePicture_UserHasNoProfilePicture_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync($"/api/Media/{_user.Id}/profile-picture");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProfilePicture_UserDoesNotExist_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync("/api/Media/999999/profile-picture");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProfilePicture_NotLoggedIn_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync($"/api/Media/{_user.Id}/profile-picture");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task GetTaskAttachment_OwnAttachment_ReturnsOkWithFile()
    {
        TaskAttachment attachment = await UploadAttachmentAsCurrentUser();

        var response = await HttpClient.GetAsync($"/api/Media/task-attachments/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        response.Headers.ETag.ShouldNotBeNull();
        response.Content.Headers.ContentDisposition.ShouldBeNull();
    }


    [Fact]
    public async Task GetTaskAttachment_AttachmentDoesNotExist_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync("/api/Media/task-attachments/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTaskAttachment_AttachmentBelongsToDifferentUser_ReturnsNotFound()
    {
        User otherUser = await CreateOtherUser();
        TaskAttachment attachment = await CreateAttachmentInDb(otherUser.Id);

        var response = await HttpClient.GetAsync($"/api/Media/task-attachments/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTaskAttachment_NotLoggedIn_ReturnsUnauthorized()
    {
        TaskAttachment attachment = await CreateAttachmentInDb(_user.Id);

        var response = await UnauthenticatedClient.GetAsync(
            $"/api/Media/task-attachments/{attachment.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
