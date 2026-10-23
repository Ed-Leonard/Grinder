namespace Grinder.Api.Services;


using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;

/// <summary>
/// Removes expired uploads that were never associated w a task
/// </summary>
public sealed class TemporaryAttachmentCleanupService : BackgroundService
{
    private static readonly TimeSpan AttachmentLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly ILogger<TemporaryAttachmentCleanupService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _uploadDir;

    public TemporaryAttachmentCleanupService(
        IDbContextFactory<DatabaseContext> dbContextFactory,
        IWebHostEnvironment hostingEnvironment,
        ILogger<TemporaryAttachmentCleanupService> logger,
        TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _timeProvider = timeProvider;
        
        string webRoot = string.IsNullOrWhiteSpace(hostingEnvironment.WebRootPath) 
            ? Path.Combine(hostingEnvironment.ContentRootPath, "wwwroot")
            : hostingEnvironment.WebRootPath;
        
        _uploadDir = Path.Combine(webRoot, "uploads", "taskAttachments");
    }
    

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DeleteExpiredAttachmentAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exc)
            {
                _logger.LogError(exc, "Temporary attachment cleanup failed");
            }

            try
            {
                await Task.Delay(CleanupInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task<int> DeleteExpiredAttachmentAsync(CancellationToken cancellationToken)
    {
        DateTime cutoff = _timeProvider.GetUtcNow().UtcDateTime - AttachmentLifetime;

        await using DatabaseContext context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var expiredAttachments = await context.TaskAttachments
            .AsNoTracking()
            .Where(attachment => attachment.TaskItemId == null && attachment.CreatedAt < cutoff)
            .Select(attachment => new { attachment.Id, attachment.StoredFileName })
            .ToListAsync(cancellationToken);

        int deletedCount = 0;

        foreach (var attachment in expiredAttachments)
        {
            // Recheck TaskItemId to avoid deleting an attachment that has just been associated w a task
            int removedRows = await context.TaskAttachments
                .Where(candidate =>
                    candidate.Id == attachment.Id &&
                    candidate.TaskItemId == null &&
                    candidate.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (removedRows == 0)
            {
                continue;
            }

            deletedCount += removedRows;

            string fileName = Path.GetFileName(attachment.StoredFileName);
            string filePath = Path.Combine(_uploadDir, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (IOException exc)
            {
                _logger.LogWarning(
                    exc,
                    "Could not delete temporary attachment file {AttachmentId}",
                    attachment.Id
                );
            }
            catch (UnauthorizedAccessException exc)
            {
                _logger.LogWarning(
                    exc,
                    "Could not access temporary attachment file {AttachmentId}",
                    attachment.Id);
            }
        }
        deletedCount += await DeleteOrphanedFilesWithNoDbRowAsync(context, cutoff, cancellationToken);
        return deletedCount;
    }

    private async Task<int> DeleteOrphanedFilesWithNoDbRowAsync(
        DatabaseContext context,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_uploadDir))
        {
            return 0;
        }

        var knownFileNames = await context.TaskAttachments
            .Select(a => a.StoredFileName)
            .ToHashSetAsync(cancellationToken);

        int deletedCount = 0;

        foreach (string filePath in Directory.EnumerateFiles(_uploadDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(filePath);

            if (knownFileNames.Contains(fileName))
            {
                continue;
            }

            if (File.GetLastWriteTimeUtc(filePath) >= cutoff)
            {
                continue; // too recent — might be mid-upload, not yet committed to DB
            }

            try
            {
                File.Delete(filePath);
                deletedCount++;
            }
            catch (IOException exc)
            {
                _logger.LogWarning(exc, "Could not delete orphaned attachment file {FileName}", fileName);
            }
            catch (UnauthorizedAccessException exc)
            {
                _logger.LogWarning(exc, "Could not access orphaned attachment file {FileName}", fileName);
            }
        }

        return deletedCount;
    }
}
