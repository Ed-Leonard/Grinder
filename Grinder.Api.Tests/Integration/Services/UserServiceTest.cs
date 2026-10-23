using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Attachments;

namespace Grinder.Api.Tests.Integration.Services;

public class UserServiceTests : BaseIntegrationTestFixture
{

    private IUserService ServiceUnderTest => ServiceProvider.GetRequiredService<IUserService>();

    public UserServiceTests(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private static IFormFile CreateMockFile(string fileName, string contentType, long size)
    {
        var content = new byte[size];
        WriteSignature(content, fileName);
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, size, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    // Stamps real magic-number bytes into the buffer (when the extension has one) so
    // AttachmentValidator's byte-signature check has genuine content to match against.
    private static void WriteSignature(byte[] content, string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        byte[]? signature = extension switch
        {
            ".png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            ".jpg" or ".jpeg" => [0xFF, 0xD8, 0xFF],
            ".gif" => "GIF89a"u8.ToArray(),
            ".webm" => [0x1A, 0x45, 0xDF, 0xA3],
            _ => null
        };

        if (signature != null && content.Length >= signature.Length)
        {
            signature.CopyTo(content, 0);
        }

        if (extension is ".mp4" or ".mov" && content.Length >= 8)
        {
            "ftyp"u8.ToArray().CopyTo(content, 4);
        }
    }

    private async Task<User> AddUserToDatabaseAsync(DatabaseContext context)
    {
        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var password = "OldPassword123!";
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            DisplayName = "Test User",
            Country = "NZ",
            PasswordHash = passwordHasher.HashPassword(null!, password),
            IsVerified = true,
            ProfilePictureUrl = "/uploads/profilePictures/original.png"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task GetUserById_ValidId_ReturnUser()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var foundUser = await ServiceUnderTest.GetUserByIdAsync(user.Id);

        foundUser.ShouldBeEquivalentTo(user);
    }

    [Fact]
    public async Task GetUserById_InvalidId_ReturnNull()
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var foundUser = await ServiceUnderTest.GetUserByIdAsync(2);

        foundUser.ShouldBeNull();
    }

    [Fact]
    public async Task EditProfile_UnchangedEmail_DoesNotDeleteCurrentUser()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var request = new UpdateProfileRequest(user.Email, "Updated Name", user.Country);

        var result = await ServiceUnderTest.EditProfileAsync(user.Id, request);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(user.Id);

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var updatedUser = await verifyContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser.ShouldNotBeNull();
        updatedUser.Email.ShouldBe(user.Email);
        updatedUser.DisplayName.ShouldBe("Updated Name");
        updatedUser.Country.ShouldBe(user.Country);
        updatedUser.ProfilePictureUrl.ShouldBe("/uploads/profilePictures/original.png");
    }

    [Fact]
    public async Task EditProfile_EmailInUseByAnotherUser_ThrowsInvalidOperation()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var otherUser = new User
        {
            Id = 2,
            Email = "different@example.com",
            DisplayName = "Valid User",
            Country = "NZ",
            PasswordHash = user.PasswordHash,
            IsVerified = true,
        };
        context.Users.Add(otherUser);
        await context.SaveChangesAsync();

        var request = new UpdateProfileRequest(otherUser.Email, user.DisplayName, user.Country);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            ServiceUnderTest.EditProfileAsync(user.Id, request)
        );
    }

    [Fact]
    public async Task EditProfile_InvalidName_ThrowsInvalidOperation()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var request = new UpdateProfileRequest(user.Email, "    aa      ", user.Country);

        var result = await ServiceUnderTest.EditProfileAsync(user.Id, request);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task EditProfile_EmailInUseByExpiredUser_DeletesExpiredUserAndUpdatesEmail()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var expiredUser = new User
        {
            Id = 2,
            Email = "expired@example.com",
            DisplayName = "Expired User",
            Country = "NZ",
            PasswordHash = user.PasswordHash,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        context.Users.Add(expiredUser);
        await context.SaveChangesAsync();

        var request = new UpdateProfileRequest(expiredUser.Email, user.DisplayName, user.Country);

        var result = await ServiceUnderTest.EditProfileAsync(user.Id, request);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(user.Id);

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var updatedUser = await verifyContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        var expiredUserResponse = await verifyContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == expiredUser.Id);
        expiredUserResponse.ShouldBeNull();
        updatedUser.ShouldNotBeNull();
        updatedUser.Email.ShouldBe(expiredUser.Email);
        updatedUser.DisplayName.ShouldBe(user.DisplayName);
        updatedUser.Country.ShouldBe(user.Country);
        updatedUser.ProfilePictureUrl.ShouldBe("/uploads/profilePictures/original.png");
    }

    [Fact]
    public async Task UploadProfilePicture_NonExistentUser_ReturnsNull()
    {
        var file = CreateMockFile("image.png", "image/png", 1024);

        var result = await ServiceUnderTest.UploadProfilePictureAsync(999, file);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UploadProfilePicture_InvalidContentType_ThrowsValidationException()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var file = CreateMockFile("document.pdf", "application/pdf", 1024);

        var ex = await Should.ThrowAsync<AttachmentValidationException>(() =>
            ServiceUnderTest.UploadProfilePictureAsync(user.Id, file)
        );
        ex.Error.ShouldBe(AttachmentValidationError.UnsupportedImageType);
    }

    [Fact]
    public async Task UploadProfilePicture_FileTooLarge_ThrowsValidationException()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var file = CreateMockFile("image.png", "image/png", 6 * 1024 * 1024);

        var ex = await Should.ThrowAsync<AttachmentValidationException>(() =>
            ServiceUnderTest.UploadProfilePictureAsync(user.Id, file)
        );
        ex.Error.ShouldBe(AttachmentValidationError.ImageTooLarge);
    }

    [Fact]
    public async Task UploadProfilePicture_ValidFile_UpdatesProfilePictureUrl()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var file = CreateMockFile("image.png", "image/png", 1024);

        var result = await ServiceUnderTest.UploadProfilePictureAsync(user.Id, file);

        result.ShouldNotBeNull();
        result.ProfilePictureUrl.ShouldNotBeNullOrEmpty();
        result.ProfilePictureUrl.ShouldStartWith("/uploads/profilePictures/");

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var updatedUser = await dbContext.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.ProfilePictureUrl.ShouldBe(result.ProfilePictureUrl);
    }

    [Fact]
    public async Task UpdatePassword_ValidRequest_UpdatesPassword()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        await ServiceUnderTest.UpdatePasswordAsync(
            user.Id,
            "NewPassword123!",
            "OldPassword123!",
            "NewPassword123!"
        );

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        var updatedUser = await dbContext.Users.FirstAsync(u => u.Id == user.Id);

        var result = passwordHasher.VerifyHashedPassword(
            updatedUser,
            updatedUser.PasswordHash,
            "NewPassword123!"
        );

        result.ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task UpdatePassword_IncorrectOldPassword_ThrowsUnauthorizedAccessException()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(() =>
            ServiceUnderTest.UpdatePasswordAsync(
                user.Id,
                "NewPassword123!",
                "WrongPassword123!",
                "NewPassword123!"
            )
        );

        ex.Message.ShouldBe("Old password does not match password on file");
    }

    [Fact]
    public async Task UpdatePassword_PasswordsDoNotMatch_ThrowsArgumentException()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.UpdatePasswordAsync(
                user.Id,
                "NewPassword123!",
                "OldPassword123!",
                "DifferentPassword123!"
            )
        );

        ex.Message.ShouldBe("Passwords do not match");
    }

    [Fact]
    public async Task UpdatePassword_InvalidNewPassword_ThrowsArgumentException()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        var ex = await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.UpdatePasswordAsync(
                user.Id,
                "weak",
                "OldPassword123!",
                "weak"
            )
        );

        ex.Message.ShouldContain("Password must be at least 8 characters long");
    }

    [Fact]
    public async Task UpdatePassword_UserDoesNotExist_ThrowsKeyNotFoundException()
    {
        var ex = await Should.ThrowAsync<KeyNotFoundException>(() =>
            ServiceUnderTest.UpdatePasswordAsync(
                999,
                "NewPassword123!",
                "OldPassword123!",
                "NewPassword123!"
            )
        );

        ex.Message.ShouldBe("User not found.");
    }

    [Fact]
    public async Task UploadProfilePicture_ContentTypeAndExtensionMismatch_ThrowsArgumentException()
    {
        var context = await DbContextFactory.CreateDbContextAsync();
        var user = await AddUserToDatabaseAsync(context);

        // Create a .png file with content type of a jpeg image
        var file = CreateMockFile("image.png", "image/jpeg", 1024);

        var ex = await Should.ThrowAsync<AttachmentValidationException>(() =>
            ServiceUnderTest.UploadProfilePictureAsync(user.Id, file)
        );
    }

}
