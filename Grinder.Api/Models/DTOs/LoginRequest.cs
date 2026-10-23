using System.ComponentModel.DataAnnotations;
using Grinder.Api.Utilities;

namespace Grinder.Api.Models.DTOs
{
    /// <summary>
    /// Gives the required data for a login attempt.
    /// </summary>
    public record LoginRequest
    (
        [Required(ErrorMessage = "Email is required.")]
        [ValidEmail(ErrorMessage = "Invalid email address. Email must be in the format 'jane@doe.nz'")]
        string Email,

        [Required(ErrorMessage = "Password is required.")]
        string Password
    );
}
