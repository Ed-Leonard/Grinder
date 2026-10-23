using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.DTOs
{
    public record UpdatePasswordRequest(
        string NewPassword,
        string OldPassword,
        string ConfirmPassword
    );

}
