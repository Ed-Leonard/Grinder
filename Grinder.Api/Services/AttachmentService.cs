using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Attachments;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.StaticFiles;

namespace Grinder.Api.Services;

public interface IAttachmentService
{
    Task<TaskAttachment> UploadTemporaryAsync(
        int userId,
        IFormFile file,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default
    );

    Task<string> UploadProfilePictureAsync(
        IFormFile file,
        CancellationToken cancellationToken = default
    );

    Task<bool> DeleteAsync(
        int userId,
        int attachmentId,
        CancellationToken cancellationToken = default
    );

    StoredFile? ResolveProfilePicture(string storedUrl);
    Task<StoredFile?> GetTaskAttachmentFileAsync(
        int userId,
        int attachmentId,
        CancellationToken cancellationToken = default
    );
}

public sealed class AttachmentService : IAttachmentService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IAttachmentValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AttachmentService> _logger;
    private readonly string _taskAttachmentsUploadDirectory;
    private readonly string _profilePictureUploadDirectory;
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    public AttachmentService(
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IAttachmentValidator validator,
        IWebHostEnvironment hostEnvironment,
        TimeProvider timeProvider,
        ILogger<AttachmentService> logger
    )
    {
        _dbContextFactory = dbContextFactory;
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;

        string webRoot = string.IsNullOrWhiteSpace(hostEnvironment.WebRootPath)
            ? Path.Combine(hostEnvironment.ContentRootPath, "wwwroot")
            : hostEnvironment.WebRootPath;

        _taskAttachmentsUploadDirectory = Path.Combine(webRoot, "uploads", "taskAttachments");
        _profilePictureUploadDirectory = Path.Combine(webRoot, "uploads", "profilePictures");
    }

    public async Task<TaskAttachment> UploadTemporaryAsync(
        int userId,
        IFormFile file,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default
    )
    {
        ValidatedAttachment validatedFile = await _validator.ValidateAsync(file);

        await using DatabaseContext context =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        bool userExists = await context.Users.AnyAsync(
            user => user.Id == userId,
            cancellationToken
        );

        if (!userExists)
        {
            throw new KeyNotFoundException("User not found");
        }

        Directory.CreateDirectory(_taskAttachmentsUploadDirectory);

        string storedFileName = $"{Guid.NewGuid():N}{validatedFile.Extension}";
        string filePath = Path.Combine(_taskAttachmentsUploadDirectory, storedFileName);

        try
        {
            await using (FileStream stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            ))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            bool isPlausible = width is > 0 and <= 20000 && height is > 0 and <= 20000;

            TaskAttachment attachment = new TaskAttachment
            {
                TaskItemId = null,
                UserId = userId,
                StoredFileName = storedFileName,
                ContentType = validatedFile.ContentType,
                FileSize = validatedFile.FileSize,
                Url = $"/uploads/taskAttachments/{storedFileName}",
                width = isPlausible ? width : null,
                height = isPlausible ? height : null,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            context.TaskAttachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken);

            return attachment;
        }
        catch
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            throw;
        }
    }

    public async Task<string> UploadProfilePictureAsync(
        IFormFile file,
        CancellationToken cancellationToken = default
    )
    {
        ValidatedAttachment validatedFile = await _validator.ValidateAsync(file);

        if (validatedFile.Kind != AttachmentKind.Image)
        {
            throw new AttachmentValidationException(
                AttachmentValidationError.UnsupportedImageType
            );
        }

        Directory.CreateDirectory(_profilePictureUploadDirectory);

        string storedFileName = $"{Guid.NewGuid():N}{validatedFile.Extension}";
        string filePath = Path.Combine(_profilePictureUploadDirectory, storedFileName);

        try
        {
            await using FileStream stream = new(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );

            await file.CopyToAsync(stream, cancellationToken);
        }
        catch
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            throw;
        }
        return $"/uploads/profilePictures/{storedFileName}";
    }

    public async Task<bool> DeleteAsync(
        int userId,
        int attachmentId,
        CancellationToken cancellationToken = default
    )
    {
        await using DatabaseContext context =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        TaskAttachment? attachment = await context.TaskAttachments.SingleOrDefaultAsync(
            item => item.Id == attachmentId && item.UserId == userId,
            cancellationToken
        );

        if (attachment == null)
        {
            return false;
        }

        context.TaskAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken);

        string safeStoredFileName = Path.GetFileName(attachment.StoredFileName);
        string filePath = Path.Combine(_taskAttachmentsUploadDirectory, safeStoredFileName);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not delete attachment file {AttachmentId}",
                attachment.Id
            );
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(
                exception,
                "Could not access attachment file {AttachmentId}",
                attachment.Id
            );
        }

        return true;
    }

    private StoredFile? ResolveFile(
        string uploadDirectory,
        string storedFileName
    )
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            return null;
        }

        string safeName = Path.GetFileName(storedFileName);
        string physicalPath = Path.Combine(uploadDirectory, safeName);

        if (!File.Exists(physicalPath))
        {
            return null;
        }

        string contentType = ContentTypeProvider.TryGetContentType(safeName, out string? resolved)
            ? resolved
            : "application/octet-stream";

        return new StoredFile(physicalPath, contentType, safeName);
    }

    public StoredFile? ResolveProfilePicture(string storedUrl)
        => ResolveFile(_profilePictureUploadDirectory, storedUrl);

    public async Task<StoredFile?> GetTaskAttachmentFileAsync(
        int userId,
        int attachmentId,
        CancellationToken cancellationToken = default
    )
    {
        await using DatabaseContext context =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var attachment = await context
            .TaskAttachments.Where(item => item.Id == attachmentId)
            .Select(item => new
            {
                item.StoredFileName,
                item.UserId,
                TaskVisibility = item.Task != null ? (TaskVisibility?)item.Task.Visibility : null
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null)
        {
            return null;
        }

        bool isOwner = attachment.UserId == userId;
        bool isVisibleToViewer = isOwner
            || attachment.TaskVisibility == TaskVisibility.Public
            || (attachment.TaskVisibility == TaskVisibility.FriendsOnly
                && await context.Follow.AnyAsync(
                    f => f.FollowerId == userId
                        && f.FollowedId == attachment.UserId
                        && context.Follow.Any(reverse =>
                            reverse.FollowerId == attachment.UserId
                            && reverse.FollowedId == userId),
                    cancellationToken));

        return isVisibleToViewer
            ? ResolveFile(_taskAttachmentsUploadDirectory, attachment.StoredFileName)
            : null;
    }

}
