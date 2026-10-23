using System.ComponentModel.DataAnnotations;
using Grinder.Api.Utilities;

namespace Grinder.Api.Models.DTOs
{

	public record RegisterRequest(

	[Required(ErrorMessage = "Email is required.")]
	[ValidEmail(ErrorMessage = "Invalid email address.")]
	string Email,

	[Required(ErrorMessage = "Display name is required.")]
	[MinLength(3, ErrorMessage = "Display name must be between 3 and 64 characters.")]
	[MaxLength(64, ErrorMessage = "Display name must be between 3 and 64 characters.")]
	[RegularExpression(@"^[\p{L}\s\-']+$", ErrorMessage = "Display name must only include letters, spaces, hyphens or apostrophes")]
	string DisplayName,

	[Required(ErrorMessage = "Password is required.")]
	[RegularExpression(
		@"^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$",
		ErrorMessage = "Password must be at least 8 characters long including at least one of each uppercase, lowercase, numbers and special characters."
	)]
	string Password,

	[Required(ErrorMessage = "Country is required.")]
	string Country
	);
}
