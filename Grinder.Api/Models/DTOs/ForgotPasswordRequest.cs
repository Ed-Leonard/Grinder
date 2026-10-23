using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.DTOs;

public record ForgotPasswordRequest
(
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address. Email must be in the format 'user@domain.com'.")]
    string Email
);
