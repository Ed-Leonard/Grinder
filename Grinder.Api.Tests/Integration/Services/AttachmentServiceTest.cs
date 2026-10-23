namespace Grinder.Api.Tests.Integration.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Attachments;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

public class AttachmentServiceTest : BaseIntegrationTestFixture
{
    private readonly string _webRootPath;

    public AttachmentServiceTest(WebApplicationFactory<Program> webApplicationFactory)
        : base(webApplicationFactory)
    {
        _webRootPath = Path.Combine(
            Path.GetTempPath(),
            $"attachment-tests-{Guid.NewGuid():N}"
        );
    }

    [Fact]
    public async Task UploadProfilePictureAsync_ValidImage_StoresPermanentFile()
    {
        try
        {
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "profile.png",
                "image/png"
            );

            string result = await service.UploadProfilePictureAsync(file);

            result.ShouldStartWith("/uploads/profilePictures/");

            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "profilePictures",
                Path.GetFileName(result)
            );

            File.Exists(storedPath).ShouldBeTrue();

            await using DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync();

            context.TaskAttachments.ShouldBeEmpty();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task UploadTemporaryAsync_ValidImage_PersistsMetadataAndFile()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "my-picture.png",
                "image/png"
            );

            TaskAttachment result = await service.UploadTemporaryAsync(user.Id, file);

            result.Id.ShouldBeGreaterThan(0);
            result.TaskItemId.ShouldBeNull();
            result.ContentType.ShouldBe("image/png");
            result.FileSize.ShouldBe(16);
            result.Url.ShouldStartWith("/uploads/taskAttachments/");
            result.StoredFileName.ShouldEndWith(".png");

            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "taskAttachments",
                result.StoredFileName
            );

            File.Exists(storedPath).ShouldBeTrue();

            await using DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync();

            TaskAttachment storedAttachment =
                await context.TaskAttachments.SingleAsync();

            storedAttachment.Id.ShouldBe(result.Id);
            storedAttachment.UserId.ShouldBe(user.Id);
            storedAttachment.TaskItemId.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task UploadTemporaryAsync_UnknownUser_DoesNotCreateAttachment()
    {
        try
        {
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "my-picture.png",
                "image/png"
            );

            await Should.ThrowAsync<KeyNotFoundException>(
                () => service.UploadTemporaryAsync(999999, file)
            );

            await using DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync();

            context.TaskAttachments.ShouldBeEmpty();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    private IAttachmentService CreateService()
    {
        IWebHostEnvironment environment =
            Substitute.For<IWebHostEnvironment>();

        environment.WebRootPath.Returns(_webRootPath);
        environment.ContentRootPath.Returns(_webRootPath);

        return new AttachmentService(
            DbContextFactory,
            new AttachmentValidator(),
            environment,
            FakeTimeProvider,
            NullLogger<AttachmentService>.Instance
        );
    }

    private async Task<User> AddUserAsync(
        string email = "test@example.com")
    {
        await using DatabaseContext context = await DbContextFactory.CreateDbContextAsync();

        User user = new()
        {
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            IsVerified = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private static IFormFile CreateFile(
        string fileName,
        string contentType)
    {
        byte[] content = ContentBytesFor(fileName);
        MemoryStream stream = new(content);

        return new FormFile(
            stream,
            0,
            content.Length,
            "file",
            fileName
        )
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    // Builds real magic-number bytes for the file's extension so AttachmentValidator's
    // byte-signature check has genuine content to match against.
    private static byte[] ContentBytesFor(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".png" => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0),
            ".jpg" or ".jpeg" => Pad([0xFF, 0xD8, 0xFF], 0),
            ".gif" => Pad("GIF89a"u8.ToArray(), 0),
            ".svg" => "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray(),
            ".mp4" or ".mov" => Pad("ftyp"u8.ToArray(), 4),
            ".webm" => Pad([0x1A, 0x45, 0xDF, 0xA3], 0),
            _ => [1, 2, 3, 4]
        };
    }

    private static byte[] Pad(byte[] signature, int offset)
    {
        byte[] buffer = new byte[16];
        signature.CopyTo(buffer, offset);
        return buffer;
    }

    [Fact]
    public async Task DeleteAsync_OwnPendingAttachment_RemovesRowAndFile()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "my-picture.gif",
                "image/gif"
            );

            TaskAttachment attachment =
                await service.UploadTemporaryAsync(user.Id, file);

            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "taskAttachments",
                attachment.StoredFileName
            );

            File.Exists(storedPath).ShouldBeTrue();

            bool deleted = await service.DeleteAsync(
                user.Id,
                attachment.Id
            );

            deleted.ShouldBeTrue();
            File.Exists(storedPath).ShouldBeFalse();

            await using DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync();

            context.TaskAttachments.ShouldBeEmpty();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task DeleteAsync_DifferentUser_DoesNotDeleteAttachment()
    {
        try
        {
            User owner = await AddUserAsync("owner@example.com");
            User otherUser = await AddUserAsync("other@example.com");
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "my-picture.svg",
                "image/svg+xml"
            );

            TaskAttachment attachment =
                await service.UploadTemporaryAsync(owner.Id, file);

            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "taskAttachments",
                attachment.StoredFileName
            );

            bool deleted = await service.DeleteAsync(
                otherUser.Id,
                attachment.Id
            );

            deleted.ShouldBeFalse();
            File.Exists(storedPath).ShouldBeTrue();

            await using DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync();

            TaskAttachment storedAttachment =
                context.TaskAttachments.ShouldHaveSingleItem();

            storedAttachment.Id.ShouldBe(attachment.Id);
            storedAttachment.UserId.ShouldBe(owner.Id);
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task DeleteAsync_OwnLinkedAttachment_RemovesRowAndFile()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "linked-image.png",
                "image/png"
            );

            TaskAttachment attachment =
                await service.UploadTemporaryAsync(user.Id, file);

            // Link the attachment to a task
            await using (DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync())
            {

                TaskItem task = new()
                {
                    UserId = user.Id,
                    Title = "Test Task",
                    ItemStatus = TaskItemStatus.Todo,
                    DueDate = DateOnly.FromDateTime(TestNow.AddDays(2).DateTime),
                };
                context.TaskItems.Add(task);
                await context.SaveChangesAsync();

                TaskAttachment dbAttachment =
                    await context.TaskAttachments.SingleAsync(a => a.Id == attachment.Id);
                dbAttachment.TaskItemId = task.Id;
                await context.SaveChangesAsync();
            }

            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "taskAttachments",
                attachment.StoredFileName
            );

            File.Exists(storedPath).ShouldBeTrue();

            bool deleted = await service.DeleteAsync(
                user.Id,
                attachment.Id
            );

            deleted.ShouldBeTrue();
            File.Exists(storedPath).ShouldBeFalse();

            await using DatabaseContext verifyContext =
                await DbContextFactory.CreateDbContextAsync();

            verifyContext.TaskAttachments.ShouldBeEmpty();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task DeleteAsync_AttachmentDoesNotExist_ReturnsFalse()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();

            bool deleted = await service.DeleteAsync(
                user.Id,
                999999
            );

            deleted.ShouldBeFalse();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task DeleteAsync_FileNotOnDisk_RemovesDbRecordAndReturnTrue()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "orphan-file.png",
                "image/png"
            );

            TaskAttachment attachment =
                await service.UploadTemporaryAsync(user.Id, file);

            // Manually delete the file from disk to simulate missing file
            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "taskAttachments",
                attachment.StoredFileName
            );

            File.Delete(storedPath);
            File.Exists(storedPath).ShouldBeFalse();

            bool deleted = await service.DeleteAsync(
                user.Id,
                attachment.Id
            );

            deleted.ShouldBeTrue();

            await using DatabaseContext context =
                await DbContextFactory.CreateDbContextAsync();

            context.TaskAttachments.ShouldBeEmpty();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public void ResolveProfilePicture_FileExists_ReturnsStoredFileWithMatchingContentType()
    {
        try
        {
            IAttachmentService service = CreateService();
            string profileDirectory = Path.Combine(_webRootPath, "uploads", "profilePictures");
            Directory.CreateDirectory(profileDirectory);
            string storedFileName = $"{Guid.NewGuid():N}.png";
            File.WriteAllBytes(Path.Combine(profileDirectory, storedFileName), [1, 2, 3, 4]);

            StoredFile? result = service.ResolveProfilePicture($"/uploads/profilePictures/{storedFileName}");

            result.ShouldNotBeNull();
            result.ContentType.ShouldBe("image/png");
            result.PhysicalPath.ShouldBe(Path.Combine(profileDirectory, storedFileName));
            result.Version.ShouldBe(storedFileName);
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public void ResolveProfilePicture_FileMissingFromDisk_ReturnsNull()
    {
        try
        {
            IAttachmentService service = CreateService();

            StoredFile? result = service.ResolveProfilePicture(
                "/uploads/profilePictures/does-not-exist.png"
            );

            result.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolveProfilePicture_BlankStoredUrl_ReturnsNull(string? storedUrl)
    {
        try
        {
            IAttachmentService service = CreateService();

            StoredFile? result = service.ResolveProfilePicture(storedUrl!);

            result.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public void ResolveProfilePicture_PathTraversalAttempt_DoesNotEscapeUploadDirectory()
    {
        try
        {
            IAttachmentService service = CreateService();

            StoredFile? result = service.ResolveProfilePicture("../../../../etc/passwd");

            result.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }


    [Fact]
    public async Task GetTaskAttachmentFileAsync_OwnAttachment_ReturnsStoredFile()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "vacation-photo.png",
                "image/png"
            );

            TaskAttachment attachment = await service.UploadTemporaryAsync(user.Id, file);

            StoredFile? result = await service.GetTaskAttachmentFileAsync(user.Id, attachment.Id);

            result.ShouldNotBeNull();
            result.ContentType.ShouldBe("image/png");
            result.Version.ShouldBe(attachment.StoredFileName);
            result.PhysicalPath.ShouldBe(
                Path.Combine(_webRootPath, "uploads", "taskAttachments", attachment.StoredFileName)
            );
            File.Exists(result.PhysicalPath).ShouldBeTrue();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task GetTaskAttachmentFileAsync_DifferentUser_ReturnsNull()
    {
        try
        {
            User owner = await AddUserAsync("owner@example.com");
            User otherUser = await AddUserAsync("other@example.com");
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "my-picture.png",
                "image/png"
            );

            TaskAttachment attachment = await service.UploadTemporaryAsync(owner.Id, file);

            StoredFile? result = await service.GetTaskAttachmentFileAsync(
                otherUser.Id,
                attachment.Id
            );

            result.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task GetTaskAttachmentFileAsync_AttachmentDoesNotExist_ReturnsNull()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();

            StoredFile? result = await service.GetTaskAttachmentFileAsync(user.Id, 999999);

            result.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task GetTaskAttachmentFileAsync_FileMissingFromDisk_ReturnsNull()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "orphan-file.png",
                "image/png"
            );

            TaskAttachment attachment = await service.UploadTemporaryAsync(user.Id, file);

            string storedPath = Path.Combine(
                _webRootPath,
                "uploads",
                "taskAttachments",
                attachment.StoredFileName
            );
            File.Delete(storedPath);

            StoredFile? result = await service.GetTaskAttachmentFileAsync(user.Id, attachment.Id);

            result.ShouldBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    [Fact]
    public async Task GetTaskAttachmentFileAsync_LinkedAttachment_StillResolvesFile()
    {
        try
        {
            User user = await AddUserAsync();
            IAttachmentService service = CreateService();
            IFormFile file = CreateFile(
                "linked-image.png",
                "image/png"
            );

            TaskAttachment attachment = await service.UploadTemporaryAsync(user.Id, file);

            await using (DatabaseContext context = await DbContextFactory.CreateDbContextAsync())
            {
                TaskItem task = new()
                {
                    UserId = user.Id,
                    Title = "Test Task",
                    ItemStatus = TaskItemStatus.Todo,
                    DueDate = DateOnly.FromDateTime(TestNow.AddDays(2).DateTime),
                };
                context.TaskItems.Add(task);
                await context.SaveChangesAsync();

                TaskAttachment dbAttachment =
                    await context.TaskAttachments.SingleAsync(a => a.Id == attachment.Id);
                dbAttachment.TaskItemId = task.Id;
                await context.SaveChangesAsync();
            }

            StoredFile? result = await service.GetTaskAttachmentFileAsync(user.Id, attachment.Id);

            result.ShouldNotBeNull();
        }
        finally
        {
            DeleteTestFiles();
        }
    }

    private void DeleteTestFiles()
    {
        if (Directory.Exists(_webRootPath))
        {
            Directory.Delete(_webRootPath, recursive: true);
        }
    }
}
