using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Services;

/// <summary>
/// Defines operations for generating and storing verification codes.
/// </summary>
public interface IVerificationCodeService
{
    Task GenerateAndStoreCodeAsync(string email, CodeType type);
    Task<DateTimeOffset> VerifyCodeAsync(string email, string code, CodeType type);
    Task CancelPasswordResetAsync(string email);
    Task SendEmail(string email, CodeType type, string code, DateTimeOffset? expiry = null);
}

/// <summary>
/// Service implementation responsible for generating verification codes
/// and storing them in the database.
/// </summary>
public class VerificationCodeService : IVerificationCodeService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IEmailService _emailService;

    public VerificationCodeService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider, IEmailService emailService)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _emailService = emailService;
    }

    /// <summary>
    /// Generates a new verification code for a user and stores it in the database.
    /// Any existing unused codes of the same type are removed before storing the new code.
    /// </summary>
    /// <param name="email">
    /// The email address of the user the verification code belongs to.
    /// </param>
    /// <param name="type">
    /// The type of verification code to generate (e.g., EmailVerification, PasswordReset).
    /// </param>
    /// <returns>
    /// The newly created VerificationCode entity.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the user does not exist or if the account has expired and been deleted.
    /// </exception>
    public async Task GenerateAndStoreCodeAsync(string email, CodeType type)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            var userDeleted = await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email) != null;
            if (userDeleted)
            {
                throw new ArgumentException("Account no longer exists");
            }
            else
            {
                throw new ArgumentException("User does not exist", nameof(email));
            }
        }

        var existingCode = await context.VerificationCodes
            .FirstOrDefaultAsync(vc => vc.UserId == user.Id && vc.Type == type);

        var verificationCode = new VerificationCode
        {
            Code = GenerateSixDigitCode(),
            ExpiresAt = new DateTimeOffset(_timeProvider.GetUtcNow().UtcDateTime, TimeSpan.Zero).AddMinutes(5),
            Type = type,
            UserId = user.Id
        };

        if (existingCode != null)
        {
            if (type == CodeType.EmailVerification)
            {
                verificationCode.ExpiresAt = existingCode.ExpiresAt;
            }
            context.VerificationCodes.Remove(existingCode);
        }

        context.VerificationCodes.Add(verificationCode);
        await context.SaveChangesAsync();
        SendEmail(user.Email, verificationCode.Type, verificationCode.Code, verificationCode.ExpiresAt); // don't await this, we don't want to delay the response if email sending is slow
    }

    /// <summary>
    /// Verifies a provided verification code for a user.
    /// </summary>
    /// <param name="email">
    /// The email address of the user attempting verification.
    /// </param>
    /// <param name="code">
    /// The verification code provided by the user.
    /// </param>
    /// <param name="type">
    /// The type of verification code being validated.
    /// <exception cref="ArgumentException">
    /// Thrown if the user does not exist or if the account has expired and been deleted.
    /// </exception>
    public async Task<DateTimeOffset> VerifyCodeAsync(string email, string code, CodeType type)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !user.IsVerified && user.CreatedAt < DateTime.UtcNow.AddMinutes(-5))
            throw new ArgumentException("Code is no longer valid, account no longer exists");

        var verificationCode = await context.VerificationCodes
            .Include(vs => vs.User)
            .IgnoreQueryFilters()
            .Where(vc => vc.User.Email == email
                    && vc.Type == type
                    && vc.Code == code)
            .FirstOrDefaultAsync();

        if (verificationCode == null) throw new ArgumentException("Invalid code");

        if (verificationCode.ExpiresAt < _timeProvider.GetUtcNow())
        {
            context.VerificationCodes.Remove(verificationCode);
            await context.SaveChangesAsync();
            if (!user.IsVerified)
                throw new ArgumentException("Code is no longer valid, account no longer exists");
            else
                throw new ArgumentException("Code is no longer valid, please ask for a new code.");
        }

        if (!user.IsVerified && type == CodeType.EmailVerification)
            user.IsVerified = true;

        var expiry = verificationCode.ExpiresAt;

        context.VerificationCodes.Remove(verificationCode);
        await context.SaveChangesAsync();

        return expiry;
    }

    /// <summary>
    /// Generates a random 6-digit numeric Code.
    /// </summary>
    /// <returns>A 6-digit code as a string.</returns>
    internal static string GenerateSixDigitCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
    }

    /// <summary>
    /// Removes any active reset password token from the database
    /// </summary>
    /// <param name="email">
    /// The email of the user who is requesting cancellation
    /// </param>
    public async Task CancelPasswordResetAsync(string email)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            return;
        }

        var resetCode = await context.VerificationCodes
            .FirstOrDefaultAsync(vc => vc.UserId == user.Id && vc.Type == CodeType.PasswordReset);

        if (resetCode == null)
        {
            return;
        }

        context.VerificationCodes.Remove(resetCode);
        await context.SaveChangesAsync();

        await SendEmail(email, CodeType.PasswordResetCancellation, "");
    }

    /// <summary>
    /// Builds the email in one place for a particular code type
    /// </summary>
    /// <param name="email">
    /// The email of the user who is recieving the email
    /// </param>
    /// <param name="type">
    /// The codetype the email is for
    /// </param>
	/// <param name="code">
    /// The actual verification code to be placed in the email body if required
    /// </param>
    /// <param name="expiry">
    /// The expiry time of the code, used for email types that include expiry information
    /// </param>
    public async Task SendEmail(string email, CodeType type, string code, DateTimeOffset? expiry = null)
    {
        var subject = GenerateEmailSubject(type);
        string? body;
        if (expiry != null)
        {
            var minutesRemaining = (int)Math.Ceiling((expiry.Value - _timeProvider.GetUtcNow()).TotalMinutes);
            body = GenerateEmailBody(type, code, minutesRemaining);
        }
        else
        {
            body = GenerateEmailBody(type, code);
        }
        _emailService.SendEmailAsync(email, subject, body);
    }

    /// <summary>
    /// Generates the email subject for the given verification code type.
    /// </summary>
    /// <param name="type">
    /// The type of verification code being sent.
    /// </param>
    /// <returns>
    /// A user-friendly email subject line.
    /// </returns>
    internal static string GenerateEmailSubject(CodeType type)
    {
        return type switch
        {
            CodeType.EmailVerification => "Verify your email address",
            CodeType.PasswordReset => "Reset your password",
            CodeType.PasswordUpdate => "Confirm your password change",
            CodeType.PasswordResetCancellation => "Password reset cancelled",
            _ => "Your verification code"
        };
    }

    /// <summary>
    /// Generates the email body for the given verification code type.
    /// </summary>
    /// <param name="type">
    /// The type of verification code being sent.
    /// </param>
    /// <param name="code">
    /// The six-digit verification code to include in the email.
    /// </param>
    /// <param name="minutesRemaining">
    /// The number of minutes until the code expires, used for email types that include expiry information
    /// </param>
    /// <returns>
    /// A formatted email body containing instructions and the verification code.
    /// </returns>
    internal static string GenerateEmailBody(CodeType type, string code, int minutesRemaining = 5)
    {
        return type switch
        {
            CodeType.EmailVerification =>
                $"""
            Hello,

            Thank you for registering.

            Your email verification code is: {code}

            This code will expire in {minutesRemaining} minutes.
            """,

            CodeType.PasswordReset =>
                $"""
            Hello,

            Your password reset code is: {code}

            This code will expire in {minutesRemaining} minutes.
            """,

            CodeType.PasswordResetCancellation =>
            """
			Hello,

			Your password reset process has been cancelled. If you did not request this, please contact support immediately.
			""",

            CodeType.PasswordUpdate =>
                $"""
            Hello,

            Your update password code is: {code}

            This code will expire in {minutesRemaining} minutes.
            """,
            _ =>
                    $"""
            Hello,

            Your verification code is: {code}

            This code will expire in {minutesRemaining} minutes.
            """,
        };

    }
}
