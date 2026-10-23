using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Models.Entities;
// <summary>
// Represents a user profile.
// Note: added explicit [Required] attributes to make code clearer, even though non-nullable reference types should enforce this as well.
// </summary>
[Index(nameof(Email), IsUnique = true)]
public class User
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(64)]
    [MinLength(3)]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string PasswordHash { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    public string Country { get; set; } = string.Empty;

    public bool IsVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ProfilePictureUrl { get; set; }

    public int? SelectedBadgeId { get; set; }

    public Badge? SelectedBadge { get; set; }
}
