using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class UserControllerTest : BaseIntegrationTestFixture
{

    public UserControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true)
    {
    }

    protected override void ConfigureCustomServicesForTesting(IServiceCollection services)
    {
        services.RemoveAll<IEmailService>();
        var emailServiceMock = NSubstitute.Substitute.For<IEmailService>();
        emailServiceMock
            .SendEmailAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>())
            .Returns(Task.CompletedTask);
        services.AddSingleton(emailServiceMock);
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

        var loginResponse = await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }
    private async Task<User> CreateUserAsync(string email, string displayName)
    {
        var newUser = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = "placeholder",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        newUser.PasswordHash = passwordHasher.HashPassword(newUser, "Password123!");

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(newUser);
        await dbContext.SaveChangesAsync();

        return newUser;
    }
    private async Task CreateFollowAsync(int followerId, int followedId)
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Follow.Add(new Follow
        {
            FollowerId = followerId,
            FollowedId = followedId,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    [Fact]
    public async Task GetProfile_Authenticated_ReturnsOkAndProfile()
    {
        var response = await HttpClient.GetAsync("/api/user/profile");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profileResponse = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profileResponse.ShouldNotBeNull();
        profileResponse!.Email.ShouldBe("test@test.com");
        profileResponse.DisplayName.ShouldBe("Test User");
        profileResponse.Country.ShouldBe("NZ");
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await UnauthenticatedClient.GetAsync("/api/user/profile");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task UpdateProfile_Authenticated_ReturnsOkAndUpdatesProfile()
    {

        var updateRequest = new UpdateProfileRequest("updated@ex.com", "Updated User", "Australia");
        var updateResponse = await HttpClient.PutAsJsonAsync("/api/user/profile", updateRequest);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var updatedUser = await dbContext.Users.FindAsync(_user.Id);
        updatedUser.ShouldNotBeNull();
        updatedUser!.Email.ShouldBe("updated@ex.com");
        updatedUser.DisplayName.ShouldBe("Updated User");
        updatedUser.Country.ShouldBe("Australia");
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_ReturnsUnauthorized()
    {
        var updateRequest = new UpdateProfileRequest("test@ex.com", "Test User", "New Zealand");
        var updateResponse = await UnauthenticatedClient.PutAsJsonAsync("/api/user/profile", updateRequest);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_DuplicateEmail_ReturnsConflict()
    {
        await HttpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email = "duplicate@ex.com",
            displayName = "Test User",
            password = "Password1!",
            country = "NZ"
        });
        var updateRequest = new UpdateProfileRequest("duplicate@ex.com", "Updated User", "Australia");
        var updateResponse = await HttpClient.PutAsJsonAsync("/api/user/profile", updateRequest);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdatePassword_ValidDetails_ReturnsOk()
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Type = CodeType.PasswordUpdate,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        VerifyCodeRequest verifyCode = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordUpdate);
        var verificationResponse = await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyCode);
        verificationResponse.Headers.Contains("Set-Cookie").ShouldBeTrue();
        var UpdatePasswordRequest = new UpdatePasswordRequest("NewPassword123!", "Password123!", "NewPassword123!");

        var response = await HttpClient.PutAsJsonAsync("/api/user/update-password", UpdatePasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdatePassword_InvalidNewPassword_ReturnsBadRequest()
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Type = CodeType.PasswordUpdate,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        VerifyCodeRequest verifyCode = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordUpdate);
        await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyCode);
        var UpdatePasswordRequest = new UpdatePasswordRequest("NewPassword", "Password123!", "NewPassword");

        var response = await HttpClient.PutAsJsonAsync("/api/user/update-password", UpdatePasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePassword_InvalidOldPassword_ReturnsUnauthorized()
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Type = CodeType.PasswordUpdate,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        VerifyCodeRequest verifyCode = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordUpdate);
        await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyCode);
        var UpdatePasswordRequest = new UpdatePasswordRequest("Password123!", "NewPassword123?", "Password123!");

        var response = await HttpClient.PutAsJsonAsync("/api/user/update-password", UpdatePasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePassword_NotVerified_ReturnsUnproccessableEntity()
    {
        var UpdatePasswordRequest = new UpdatePasswordRequest("Password123!", "NewPassword123!", "Password123!");
        var response = await HttpClient.PutAsJsonAsync("/api/user/update-password", UpdatePasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task UpdatePassword_NonMatchingPasswords_ReturnsBadRequest()
    {
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Type = CodeType.PasswordUpdate,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        VerifyCodeRequest verifyCode = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordUpdate);
        await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyCode);
        var UpdatePasswordRequest = new UpdatePasswordRequest("NewPassword1!", "Password123!", "NewPassword2!");

        var response = await HttpClient.PutAsJsonAsync("/api/user/update-password", UpdatePasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdatePassword_ExpiredCode_ReturnsUnproccessableEntity()
    {
        // super flaky test, relies on delaying the request to update password so that 
        // code verification works -> waits -> update password doesnt work since context expiry string is in the past
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(2),
            Type = CodeType.PasswordUpdate,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        VerifyCodeRequest verifyCode = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordUpdate);
        await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyCode);

        await Task.Delay(3000);

        var UpdatePasswordRequest = new UpdatePasswordRequest("NewPassword123!", "Password123!", "NewPassword123!");

        var response = await HttpClient.PutAsJsonAsync("/api/user/update-password", UpdatePasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".gif")]
    public async Task UploadPicture_ValidFile_ReturnsOk(string expectedExtension)
    {
        byte[] imageBytes = expectedExtension switch
        {
            ".png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            ".jpg" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46],
            ".gif" => "GIF89a"u8.ToArray(),
            _ => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
        };

        var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(imageBytes);

        string mimeType = expectedExtension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "image/png"
        };

        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
        content.Add(byteContent, "file", $"test-profile{expectedExtension}");

        var response = await HttpClient.PutAsync("/api/user/profile/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("test_file.txt")]
    [InlineData("test_file.pdf")]
    [InlineData("test_file.docx")]
    [InlineData("test_file.webp")]
    public async Task UploadPicture_InvalidFile_ReturnsBadRequest(string fileName)
    {
        var invalidContent = System.Text.Encoding.UTF8.GetBytes("This is not an image file");

        var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(invalidContent), "file", fileName }
        };

        var response = await HttpClient.PutAsync("/api/user/profile/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadPicture_FileTooLarge_ReturnsBadRequest()
    {
        // Create an image larger than 5MB (5 * 1024 * 1024 = 5242880 bytes)
        var maxSize = 5 * 1024 * 1024;
        var oversizedImageBytes = new byte[maxSize + 1];

        // Fill with PNG header and then padding
        oversizedImageBytes[0] = 0x89;
        oversizedImageBytes[1] = 0x50;
        oversizedImageBytes[2] = 0x4E;
        oversizedImageBytes[3] = 0x47;
        oversizedImageBytes[4] = 0x0D;
        oversizedImageBytes[5] = 0x0A;
        oversizedImageBytes[6] = 0x1A;
        oversizedImageBytes[7] = 0x0A;

        var content = new MultipartFormDataContent();
        var byteContent = new ByteArrayContent(oversizedImageBytes);
        byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(byteContent, "file", "test-profile-large.png");

        var response = await HttpClient.PutAsync("/api/user/profile/upload", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPublicProfile_UserNotFound_ReturnsNotFound()
    {
        var response = await HttpClient.GetAsync("/api/user/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPublicProfile_NoRelationship_ReturnsFollowStatus()
    {
        var target = await CreateUserAsync("target@test.com", "Target User");

        var response = await HttpClient.GetAsync($"/api/user/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.Id.ShouldBe(target.Id);
        profile.DisplayName.ShouldBe("Target User");
        profile.RelationshipStatus.ShouldBe(RelationshipStatus.Follow);
        profile.FriendCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetPublicProfile_CurrentUserFollowsTarget_ReturnsFollowingStatus()
    {
        var target = await CreateUserAsync("target@test.com", "Target User");

        await CreateFollowAsync(_user.Id, target.Id);

        var response = await HttpClient.GetAsync($"/api/user/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.RelationshipStatus.ShouldBe(RelationshipStatus.Following);
    }

    [Fact]
    public async Task GetPublicProfile_TargetFollowsCurrentUser_ReturnsFollowBackStatus()
    {
        var target = await CreateUserAsync("target@test.com", "Target User");

        await CreateFollowAsync(target.Id, _user.Id);

        var response = await HttpClient.GetAsync($"/api/user/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.RelationshipStatus.ShouldBe(RelationshipStatus.FollowBack);
    }

    [Fact]
    public async Task GetPublicProfile_MutualFollow_ReturnsFriendsStatus()
    {
        var target = await CreateUserAsync("target@test.com", "Target User");

        await CreateFollowAsync(_user.Id, target.Id);
        await CreateFollowAsync(target.Id, _user.Id);

        var response = await HttpClient.GetAsync($"/api/user/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.RelationshipStatus.ShouldBe(RelationshipStatus.Friends);
    }

    [Fact]
    public async Task GetPublicProfile_TargetHasFriends_ReturnsCorrectFriendCount()
    {
        var target = await CreateUserAsync("target@test.com", "Target User");
        var friendOne = await CreateUserAsync("friend1@test.com", "Friend One");
        var friendTwo = await CreateUserAsync("friend2@test.com", "Friend Two");

        await CreateFollowAsync(target.Id, friendOne.Id);
        await CreateFollowAsync(friendOne.Id, target.Id);

        await CreateFollowAsync(target.Id, friendTwo.Id);
        await CreateFollowAsync(friendTwo.Id, target.Id);

        var response = await HttpClient.GetAsync($"/api/user/{target.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.FriendCount.ShouldBe(2);
    }
}
