using Grinder.Api.Models.Entities;

namespace Grinder.Api.Models.DTOs;

/// <summary>
/// Represents a request to verify a verification code for a user.
/// </summary>
/// <param name="Email">
/// The email address of the user attempting verification.
/// </param>
/// <param name="Code">
/// The verification code that was sent to the user's email.
/// </param>
/// <param name="Type">
/// The type of verification being performed (e.g., email verification, password reset).
/// </param>
public record VerifyCodeRequest(string Email, string Code, CodeType Type);
