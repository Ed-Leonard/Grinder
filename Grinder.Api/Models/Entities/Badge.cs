using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Represents a badge that can be earned and displayed on a user's profile.
/// </summary>
public class Badge
{
   [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Icon { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string CriteriaKey { get; set; } = string.Empty;

    public int Threshold { get; set; }
}