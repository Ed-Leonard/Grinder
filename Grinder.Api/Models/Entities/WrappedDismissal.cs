using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.Entities;

/// <summary>
/// Records that a user has dismissed their Grinder Wrapped popup for a given year.
/// </summary>
public class WrappedDismissal
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }
    public int Year { get; set; }
    public DateTimeOffset DismissedAt { get; set; }
}
