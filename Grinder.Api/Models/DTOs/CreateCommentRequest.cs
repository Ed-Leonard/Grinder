using System.ComponentModel.DataAnnotations;
namespace Grinder.Api.Models.DTOs;

public class CreateCommentRequest
{
    [Required]
    public int TaskId { get; set; }

    [Required]
    [MaxLength(2048, ErrorMessage = "Comment must be 2048 characters or less")]
    public string Content { get; set; } = string.Empty;

    public int? ParentCommentId { get; set; }
}