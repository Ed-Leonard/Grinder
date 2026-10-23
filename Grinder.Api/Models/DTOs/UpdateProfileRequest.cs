using System.ComponentModel.DataAnnotations;
using Grinder.Api.Utilities;

namespace Grinder.Api.Models.DTOs;

public record UpdateProfileRequest(
	[Required(ErrorMessage = "Email is required.")]
	[ValidEmail(ErrorMessage = "Invalid email address.")]
	string Email,
	[Required(ErrorMessage = "Display name is required.")]
	[MinLength(3, ErrorMessage = "Display name must be between 3 and 64 characters.")]
	[MaxLength(64, ErrorMessage = "Display name must be between 3 and 64 characters.")]
	[RegularExpression(@"^[\p{L}\s\-']+$", ErrorMessage = "Display name must only include letters, spaces, hyphens or apostrophes")]
	string DisplayName,
	[Required(ErrorMessage = "Country is required.")]
	string Country
);
