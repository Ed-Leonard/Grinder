using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.DTOs;

public record ResetPasswordRequest(
	[Required(ErrorMessage = "Password is required.")]
	[RegularExpression(
		@"^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$",
		ErrorMessage =
			"Password must be at least 8 characters long including at least one of each uppercase, lowercase, numbers and special characters."
	)]
	string NewPassword
);
