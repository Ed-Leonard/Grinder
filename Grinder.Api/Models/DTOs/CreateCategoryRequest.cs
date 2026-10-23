using System.ComponentModel.DataAnnotations;
namespace Grinder.Api.Models.DTOs;

public record CreateCategoryRequest(
    [Required(ErrorMessage = "Name Required")]
        string Name
);
