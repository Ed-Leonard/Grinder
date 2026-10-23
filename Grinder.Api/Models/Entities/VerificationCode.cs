using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Represents the type of verification code stored in the system.
/// </summary>
/// <remarks>
/// Verification codes are used for different authentication-related actions,
/// such as confirming a user's email address or resetting a password.
/// </remarks>
public enum CodeType
{
    EmailVerification,
    PasswordUpdate,
    PasswordReset,
    PasswordResetCancellation
}

/// <summary>
/// Represents a verification code associated with a user account.
/// </summary>
public class VerificationCode
{
    [Key]
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public CodeType Type { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
