using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Models.DTOs
{
    /// <summary>
    /// Request model used when sending an email through the MailKitController.
    /// </summary>
    /// <param name="ToEmail">
    /// Email address of the recipient.
    /// </param>
    /// <param name="Subject">
    /// The subject of the email.
    /// </param>
    /// <param name="Body">
    /// The email's body content, what will actually be contained in the email.
    /// </param>
    public record SendEmailRequest(

        [Required(ErrorMessage = "Recipient email is required.")]
        [EmailAddress(ErrorMessage = "Recipient email must be a valid email address.")]
        string ToEmail,

        [Required(ErrorMessage = "Email subject is required.")]
        string Subject,

        [Required(ErrorMessage = "Email body is required.")]
        string Body
    );
}
