using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Services;

/// <summary>
/// Background service responsible for periodically removing expired verification codes from the database.
/// </summary>
/// <remarks>
/// This service runs continuously while the application is running. It checks for verification codes
/// that have passed their expiration time and deletes them from the database 
/// The cleanup task runs once every 24 hours.
/// </remarks>
public class VerificationCodeCleanupService : BackgroundService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    /// <summary>
    /// Creates a new instance of the VerificationCodeCleanupService.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Factory used to create instances of DatabaseContext for database access.
    /// </param>
    /// <param name="timeProvider">
    /// Provides the current time
    /// </param>
    public VerificationCodeCleanupService(IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Executes the background cleanup loop.
    /// </summary>
    /// <param name="stoppingToken">
    /// Token used to signal that the service should stop execution.
    /// </param>
    /// <remarks>
    /// The method repeatedly performs a cleanup of expired verification codes and then waits
    /// for 24 hours before running again. The loop continues until the application shuts down.
    /// </remarks>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DeleteExpiredCodes();

            // run once per day
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    /// <summary>
    /// Removes verification codes that have expired from the database.
    /// </summary>
    /// <remarks>
    /// A verification code is considered expired if its ExpiresAt timestamp is earlier
    /// than the current UTC time. All expired codes are deleted.
    /// </remarks>
    private async Task DeleteExpiredCodes()
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var now = DateTimeOffset.UtcNow;

        var expiredCodes = (await context.VerificationCodes.ToListAsync())
            .Where(c => c.ExpiresAt < now)
            .ToList();

        foreach (var code in expiredCodes)
        {
            if (code.Type == CodeType.EmailVerification)
            {
                var user = await context.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == code.UserId);

                if (user != null && !user.IsVerified)
                {
                    context.Users.Remove(user);
                }
            }

            context.VerificationCodes.Remove(code);
        }

        await context.SaveChangesAsync();
    }
}
