using System.Security.Claims;

namespace Grinder.Api.Utilities;

/// <summary>
/// Contains helper function to get user id from session cookie
/// </summary>
public static class Session
{
    /// <summary>
	/// Attempts to get the user id associated with a cookie claim.
	/// In practice this should always return int since [Authorize] checks if claim is valid before controller method runs
    /// </summary>
	/// <param name="context">
	/// The HttpContext passed from the api controller
	/// </param>
	/// <returns>
	/// Users id as an int if found
	///	null if no claim exists on request
	///	</returns>
    public static int? GetUserId(HttpContext context)
    {
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
