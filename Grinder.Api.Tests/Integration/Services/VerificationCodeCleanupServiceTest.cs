using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class VerificationCodeCleanupServiceTest : BaseIntegrationTestFixture
{
    public VerificationCodeCleanupServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUserToDatabaseAsync(string email = "test@example.com", bool isVerified = false)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var user = new User
        {
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = TestNow.UtcDateTime,
            IsVerified = isVerified
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task CleanupService_WhenStarted_DeletesUnverifiedUserWithExpiredEmailVerificationCode()
    {
        var user = await AddUserToDatabaseAsync(isVerified: false);

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.VerificationCodes.AddRange(
                new VerificationCode
                {
                    Code = "111111",
                    Type = CodeType.EmailVerification,
                    UserId = user.Id,
                    ExpiresAt = TestNow.AddMinutes(-10)
                },
                new VerificationCode
                {
                    Code = "222222",
                    Type = CodeType.PasswordReset,
                    UserId = user.Id,
                    ExpiresAt = TestNow.AddMinutes(10)
                }
            );

            await context.SaveChangesAsync();
        }

        var service = new VerificationCodeCleanupService(DbContextFactory);

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (TaskCanceledException)
        {
        }

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();

        verifyContext.Users.Any(u => u.Id == user.Id).ShouldBeFalse();
        verifyContext.VerificationCodes.Any(vc => vc.UserId == user.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupService_WhenStarted_DeletesExpiredNonEmailVerificationCodesOnly()
    {
        var user = await AddUserToDatabaseAsync(isVerified: false);

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.VerificationCodes.AddRange(
                new VerificationCode
                {
                    Code = "222222",
                    Type = CodeType.PasswordReset,
                    UserId = user.Id,
                    ExpiresAt = TestNow.AddMinutes(-1)
                },
                new VerificationCode
                {
                    Code = "333333",
                    Type = CodeType.PasswordUpdate,
                    UserId = user.Id,
                    ExpiresAt = TestNow.AddMinutes(10)
                }
            );

            await context.SaveChangesAsync();
        }

        var service = new VerificationCodeCleanupService(DbContextFactory);

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (TaskCanceledException)
        {
        }

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var remainingCodes = verifyContext.VerificationCodes.ToList();

        verifyContext.Users.Any(u => u.Id == user.Id).ShouldBeTrue();
        remainingCodes.Count.ShouldBe(1);
        remainingCodes.Single().Code.ShouldBe("333333");
        remainingCodes.Single().Type.ShouldBe(CodeType.PasswordUpdate);
    }

    [Fact]
    public async Task CleanupService_WhenStarted_DeletesExpiredEmailVerificationCodeButKeepsVerifiedUser()
    {
        var user = await AddUserToDatabaseAsync(isVerified: true);

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.VerificationCodes.AddRange(
                new VerificationCode
                {
                    Code = "111111",
                    Type = CodeType.EmailVerification,
                    UserId = user.Id,
                    ExpiresAt = TestNow.AddMinutes(-10)
                },
                new VerificationCode
                {
                    Code = "333333",
                    Type = CodeType.PasswordUpdate,
                    UserId = user.Id,
                    ExpiresAt = TestNow.AddMinutes(10)
                }
            );

            await context.SaveChangesAsync();
        }

        var service = new VerificationCodeCleanupService(DbContextFactory);

        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();

        try
        {
            await service.StopAsync(CancellationToken.None);
        }
        catch (TaskCanceledException)
        {
        }

        await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
        var remainingCodes = verifyContext.VerificationCodes.ToList();

        verifyContext.Users.Any(u => u.Id == user.Id).ShouldBeTrue();
        remainingCodes.Count.ShouldBe(1);
        remainingCodes.Single().Code.ShouldBe("333333");
        remainingCodes.Single().Type.ShouldBe(CodeType.PasswordUpdate);
    }
}
