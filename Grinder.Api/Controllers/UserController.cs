using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Grinder.Api.Models.Attachments;
using Grinder.Api.Utilities;

namespace Grinder.Api.Controllers;

/// <summary>
/// Handles user-related HTTP requests.
/// </summary>
/// <remarks>
/// This controller gives user-related logic to the user service and returns HTTP responses based on the results.
/// </remarks>
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IWebHostEnvironment _environment;

    private readonly IVerificationCodeService _verificationCodeService;

    private readonly IEmailService _emailService;

    private readonly IFollowService _followService;

    /// <summary>
    /// Creates a new instance of the UserController class.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Creates a database context to access the database.
    /// </param>
    /// <param name="userService">
    /// The service handling user-related business logic.
    /// </param>
    /// <param name="verificationCodeService">
    /// The service handling verification code generation and validation.
    /// </param>
    /// <param name="emailService">
    /// The service handling email sending functionality.
    /// </param>
    /// <param name="environment">
    /// The web host environment.
    /// </param>
    public UserController(IDbContextFactory<DatabaseContext> dbContextFactory, IUserService userService, IVerificationCodeService verificationCodeService, IEmailService emailService, IWebHostEnvironment environment, IFollowService followService)
    {
        _userService = userService;
        _followService = followService;
        _dbContextFactory = dbContextFactory;
        _verificationCodeService = verificationCodeService;
        _emailService = emailService;
        _environment = environment;
    }

    /// <summary>
    /// Gets users display name and email based on their credentials
    /// </summary>
    /// <returns>
    /// HTTP OK response if user is authenticated and found
    /// HTTP UNAUTHORIZED if user has invalid credentials
    /// HTTP NOTFOUND if user does not exist in database
    /// </returns>
    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        var userId = Session.GetUserId(HttpContext);

        User? user = await _userService.GetUserByIdAsync(userId!.Value);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(new ProfileResponse(user.Id, user.Email, user.DisplayName, user.Country));
    }

    /// <summary>
    /// Gets users display name, friend count and relationship to our current user
    /// </summary>
    /// <param name="id">
    /// the id of the user to be found
    /// </param>
    /// <returns>
    /// HTTP OK response if user is authenticated and found
    /// HTTP UNAUTHORIZED if user has invalid credentials
    /// HTTP NOTFOUND if user does not exist in database
    /// </returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PublicProfileResponse>> GetPublicProfile(int id)
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        User? user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        int friendCount = await _followService.GetFriendCountAsync(id);
        Dictionary<int, RelationshipStatus> relationshipStatuses = await _followService.GetRelationshipStatusesAsync(userId.Value, [id]);

        RelationshipStatus relationshipStatus = relationshipStatuses[id];

        return Ok(new PublicProfileResponse(user.Id, user.DisplayName, friendCount, relationshipStatus, user.SelectedBadge?.Name, user.SelectedBadge?.Icon));
    }

    /// <summary>
    /// Handles requests to update the profile of the currently authenticated user.
    /// </summary>
    /// <param name="request">
    /// The request containing the updated profile information.
    /// </param>
    /// <returns>
    /// HTTP OK response if the profile is updated successfully.
    /// HTTP Unauthorized if the user is not authenticated.
    /// HTTP NotFound if the user is not found.
    /// HTTP Conflict if the new email is already in use by another account.
    /// </returns>
    [HttpPut("profile")]
    public async Task<ActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = Session.GetUserId(HttpContext);

        try
        {
            User? user = await _userService.EditProfileAsync(userId!.Value, request);

            if (user == null)
            {
                return NotFound();
            }

            return Ok();
        }
        catch (InvalidOperationException)
        {
            return Conflict();
        }
    }

    /// <summary>
    /// Updates a users password to the provided one if verified and valid details
    /// </summary>
    /// <param name="request">
    /// The request containing user email, their old password and the new one they wish to update to
    /// </param>
    /// <returns>
    /// HTTP OK response if password succesfully updated
    /// HTTP Unauthorized if user is not logged in
    /// HTTP Bad Request if the input is malformed or the code is expired
    /// HTTP Not Found if user doesn't exist
    /// </returns>
    [HttpPut("update-password")]
    public async Task<ActionResult> UpdatePassword(UpdatePasswordRequest request)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }
        var user = await _userService.GetUserByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        var valid = HttpContext.Session.GetString(CodeType.PasswordUpdate.ToString());
        var expiryString = HttpContext.Session.GetString("UpdateExpiry");
        if (valid != "true" || expiryString == null)
        {
            return UnprocessableEntity("No current validation code.");
        }
        var expiry = DateTimeOffset.Parse(expiryString);

        if (DateTimeOffset.UtcNow > expiry)
        {
            HttpContext.Session.Remove(CodeType.PasswordUpdate.ToString());
            HttpContext.Session.Remove("UpdateExpiry");

            return UnprocessableEntity(new
            {
                message = "Code is no longer valid, please ask for a new code."
            });
        }

        try
        {
            await _userService.UpdatePasswordAsync(userId.Value, request.NewPassword, request.OldPassword, request.ConfirmPassword);
            HttpContext.Session.Remove(CodeType.PasswordUpdate.ToString());
            HttpContext.Session.Remove("UpdateExpiry");
            await _emailService.SendEmailAsync(user.Email, "Password successfully updated", "Your password has successfully been updated");
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Handles profile picture upload requests for authenticated users.
    /// </summary>
    /// <param name="file">
    /// The image file to be uploaded. Must be one of the allowed formats: JPEG, PNG, SVG, or GIF.
    /// Maximum file size is 5MB.
    /// </param>
    /// <returns>
    /// HTTP OK response with the updated ProfileResponse if the upload is successful.
    /// HTTP Unauthorized if the user is not authenticated.
    /// HTTP NotFound if the user does not exist in the database.
    /// HTTP BadRequest if the file format is invalid or file size exceeds the limit.
    /// </returns>
    [HttpPut("profile/upload")]
    public async Task<ActionResult<ProfileResponse>> UploadProfilePicture(IFormFile file, CancellationToken cancellationToken)
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            User? user = await _userService.UploadProfilePictureAsync(userId.Value, file, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(new ProfileResponse(user.Id, user.Email, user.DisplayName, user.Country));
        }
        catch (AttachmentValidationException exception)
        {
            return BadRequest(new
            {
                code = exception.Error.ToString(),
                message = exception.Message
            });
        }
    }
}
