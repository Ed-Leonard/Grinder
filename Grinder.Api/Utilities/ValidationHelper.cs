using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Grinder.Api.Utilities;

/// <summary>
/// Contains validator helper methods
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Runs the Validator to manually check the data annotations of a model
    /// </summary>
    /// <param name="model">
    /// The database entity to be validated
    /// </param>
    /// <returns>
    /// Boolean: True if the entity attributes are valid otherwise false
    /// </returns>
    public static bool ValidateEntity(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        bool isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return isValid;
    }


    /// <summary>
    /// Checks an email according to specifications from SENG302 handout
    /// </summary>
    /// <param name="email">
    /// the email string to be validated
    /// </param>
    /// <returns>
    /// Boolean: True if the string is a valid email otherwise false
    /// </returns>
    public static bool ValidateEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return false;
        if (email.Length > 320) return false;

        int atIndex = email.IndexOf('@');
        if (atIndex == -1 || atIndex != email.LastIndexOf('@'))
            return false;

        string local = email[..atIndex];
        string domain = email[(atIndex + 1)..];

        if (string.IsNullOrEmpty(local) || string.IsNullOrEmpty(domain)) return false;
        if (local.Length > 64) return false;
        if (domain.Length > 255) return false;

        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..")) return false;
        if (!Regex.IsMatch(local, @"^[a-zA-Z0-9!#$%&'*+/=?^_{|}`.\-]+$", RegexOptions.NonBacktracking)) return false;

        string[] labels = domain.Split('.');
        if (labels.Length < 2) return false;

        foreach (string label in labels)
        {
            if (label.Length > 63) return false;
            if (!Regex.IsMatch(label, @"^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?$", RegexOptions.NonBacktracking))
                return false;
        }

        string tld = labels[^1];
        if (Regex.IsMatch(tld, @"^\d+$", RegexOptions.NonBacktracking)) return false;

        return true;
    }

    /// <summary>   
    /// Checks a password according to specifications from SENG302 handout
    /// </summary>
    /// <param name="password">
    /// the password string to be validated
    /// </param>
    /// <returns>
    /// Boolean: True if the string is a valid password otherwise false
    /// </returns>
    public static bool ValidatePassword(string password)
    {
        return Regex.IsMatch(password, @"^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$");
    }

    /// <summary>
    /// Checks if the content type of an image is valid according to specifications from SENG302 handout
    /// </summary>
    /// <param name="contentType"></param>
    /// <returns>
    /// Boolean: True if the content type is valid otherwise false
    /// </returns>
    public static bool ValidateImageContentType(string contentType)
    {
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/svg+xml", "image/gif"};
        return allowedTypes.Contains(contentType);
    }

    /// <summary>
    /// Checks if the file size of an image is valid according to specifications from SENG302 handout
    /// </summary>
    /// <param name="fileSize"></param>
    /// <returns>    
    /// /// Boolean: True if the file size is valid otherwise false
    /// </returns>
    public static bool ValidateImageSize(long fileSize)
    {
        const long maxSize = 5 * 1024 * 1024;
        return fileSize <= maxSize;
    }
}
