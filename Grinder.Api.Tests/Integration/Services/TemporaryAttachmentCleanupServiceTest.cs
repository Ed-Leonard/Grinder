using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public sealed class TemporaryAttachmentCleanupServiceTest : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DatabaseContext> _options;
    private bool _disposed;

    public TemporaryAttachmentCleanupServiceTest()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new DatabaseContext(_options);
        context.Database.EnsureCreated();
    }

    ~TemporaryAttachmentCleanupServiceTest()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _connection.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public async Task DeleteExpiredAttachmentAsync_DeletesExpiredOrphanAttachments_OnSqlite()
    {
        var fakeTime = new FakeTimeProvider(DateTime.Parse("2026-07-26T00:00:00Z"));
        var cutoffPast = fakeTime.GetUtcNow().UtcDateTime - TimeSpan.FromDays(2); // older than AttachmentLifetime

        await using (var seedContext = new DatabaseContext(_options))
        {
            var user = new User
            {
                Email = "cleanup-test@example.com",
                DisplayName = "Cleanup Tester",
                PasswordHash = "hash",
                Country = "NZ",
                IsVerified = true,
                CreatedAt = fakeTime.GetUtcNow().UtcDateTime
            };
            seedContext.Users.Add(user);
            await seedContext.SaveChangesAsync();

            var task = new TaskItem
            {
                UserId = user.Id,
                Title = "Attached Task",
                Description = "seed",
                ItemStatus = TaskItemStatus.Todo,
                CreatedAt = fakeTime.GetUtcNow().UtcDateTime,
                Depth = 0,
                Order = 0
            };
            seedContext.TaskItems.Add(task);
            await seedContext.SaveChangesAsync();

            seedContext.TaskAttachments.Add(new TaskAttachment
            {
                TaskItemId = null,
                CreatedAt = cutoffPast,
                StoredFileName = "expired.txt"
            });
            seedContext.TaskAttachments.Add(new TaskAttachment
            {
                TaskItemId = task.Id, // associated -> must NOT be deleted
                CreatedAt = cutoffPast,
                StoredFileName = "attached.txt"
            });
            seedContext.TaskAttachments.Add(new TaskAttachment
            {
                TaskItemId = null,
                CreatedAt = fakeTime.GetUtcNow().UtcDateTime, // too recent -> must NOT be deleted
                StoredFileName = "fresh.txt"
            });
            await seedContext.SaveChangesAsync();
        }

        var dbContextFactory = new PooledDbContextFactory<DatabaseContext>(_options);
        var mockWebHostEnvironment = Substitute.For<IWebHostEnvironment>();
        var mockLogger = Substitute.For<ILogger<TemporaryAttachmentCleanupService>>();
        var service = new TemporaryAttachmentCleanupService(dbContextFactory, mockWebHostEnvironment, mockLogger, fakeTime);

        int deletedCount = await service.DeleteExpiredAttachmentAsync(CancellationToken.None);

        deletedCount.ShouldBe(1);

        await using var verifyContext = new DatabaseContext(_options);
        var remaining = await verifyContext.TaskAttachments.Select(a => a.StoredFileName).ToListAsync();
        remaining.ShouldBe(new[] { "attached.txt", "fresh.txt" });
    }

    [Fact]
    public async Task DeleteExpiredAttachmentAsync_DeletesOnlyOldOrphanedFilesWithoutDbRows()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-26T00:00:00Z"));

        string tempRoot = Path.Combine(Path.GetTempPath(), $"cleanup-test-{Guid.NewGuid():N}");
        string webRoot = Path.Combine(tempRoot, "wwwroot");
        string uploadDir = Path.Combine(webRoot, "uploads", "taskAttachments");
        Directory.CreateDirectory(uploadDir);

        string knownFileName = "known.bin";
        string oldOrphanFileName = "orphan-old.bin";
        string recentOrphanFileName = "orphan-recent.bin";

        string knownFilePath = Path.Combine(uploadDir, knownFileName);
        string oldOrphanFilePath = Path.Combine(uploadDir, oldOrphanFileName);
        string recentOrphanFilePath = Path.Combine(uploadDir, recentOrphanFileName);

        await File.WriteAllTextAsync(knownFilePath, "known");
        await File.WriteAllTextAsync(oldOrphanFilePath, "old-orphan");
        await File.WriteAllTextAsync(recentOrphanFilePath, "recent-orphan");

        DateTime nowUtc = fakeTime.GetUtcNow().UtcDateTime;
        File.SetLastWriteTimeUtc(knownFilePath, nowUtc - TimeSpan.FromDays(2));
        File.SetLastWriteTimeUtc(oldOrphanFilePath, nowUtc - TimeSpan.FromDays(2));
        File.SetLastWriteTimeUtc(recentOrphanFilePath, nowUtc - TimeSpan.FromHours(1));

        try
        {
            await using (var seedContext = new DatabaseContext(_options))
            {
                seedContext.TaskAttachments.Add(new TaskAttachment
                {
                    TaskItemId = null,
                    CreatedAt = nowUtc,
                    StoredFileName = knownFileName
                });

                await seedContext.SaveChangesAsync();
            }

            var dbContextFactory = new PooledDbContextFactory<DatabaseContext>(_options);
            var mockWebHostEnvironment = Substitute.For<IWebHostEnvironment>();
            mockWebHostEnvironment.WebRootPath.Returns(webRoot);
            mockWebHostEnvironment.ContentRootPath.Returns(tempRoot);

            var mockLogger = Substitute.For<ILogger<TemporaryAttachmentCleanupService>>();
            var service = new TemporaryAttachmentCleanupService(
                dbContextFactory,
                mockWebHostEnvironment,
                mockLogger,
                fakeTime
            );

            int deletedCount = await service.DeleteExpiredAttachmentAsync(CancellationToken.None);

            deletedCount.ShouldBe(1);
            File.Exists(oldOrphanFilePath).ShouldBeFalse();
            File.Exists(recentOrphanFilePath).ShouldBeTrue();
            File.Exists(knownFilePath).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
