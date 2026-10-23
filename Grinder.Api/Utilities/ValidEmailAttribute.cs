using System.ComponentModel.DataAnnotations;

namespace Grinder.Api.Utilities;


public class ValidEmailAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var email = value as string;
        if (ValidationHelper.ValidateEmail(email!))
            return ValidationResult.Success;
        
        return new ValidationResult(ErrorMessage ?? "Invalid email address.");
    }
}
