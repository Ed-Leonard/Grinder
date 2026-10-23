using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Grinder.Api.Controllers;

/// <summary>
/// Handles user-related HTTP requests.
/// </summary>
/// <remarks>
/// This controller gives user-related logic to the user service and returns HTTP responses based on the results.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IWebHostEnvironment _environment;

    private readonly IVerificationCodeService _verificationCodeService;

    private readonly IEmailService _emailService;

    /// <summary>
    /// Creates a new instance of the AuthController class.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Creates a database context to access the database.
    /// </param>
    /// <param name="authService">
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
    public AuthController(IDbContextFactory<DatabaseContext> dbContextFactory, IAuthService authService, IVerificationCodeService verificationCodeService, IEmailService emailService, IWebHostEnvironment environment)
    {
        _authService = authService;
        _dbContextFactory = dbContextFactory;
        _verificationCodeService = verificationCodeService;
        _emailService = emailService;
        _environment = environment;
    }

    /// <summary>
    /// Handles register requests and creates user on valid
    /// </summary>
    /// <param name="request">
    /// The register request containing email, name, password, and country
    /// </param>
    /// <returns>
    /// HTTP Created response if registration successful
    /// HTTP BadRequest response if email is invalid
    /// HTTP Conflict response if email is already in use
    /// </returns>
    /// <remarks>
    /// Preconditions:
    /// No fields may be empty

    /// Raises:
    /// An exception is thrown if the email is already in use
    /// </remarks>
    [HttpPost("register")]
    public async Task<ActionResult> RegisterUser(RegisterRequest request)
    {
        try
        {
            await _authService.RegisterAsync(request);
            HttpContext.Session.SetString(CodeType.EmailVerification.ToString(), "true");
            return Created();
        }
        catch (InvalidOperationException)
        {
            return Conflict("Email already in use.");
        }
        catch (ArgumentException)
        {
            return BadRequest("Invalid details.");
        }
    }

    /// <summary>
    /// Handles login requests using email and password credentials.
    /// </summary>
    /// <param name="request">
    /// The login request that has the email and password used for the login attempt.
    /// </param>
    /// <returns>
    /// HTTP OK response and a LoginResponse if the login attempt is valid.
    /// HTTP Unauthorized if the login attempt is invalid.
    /// </returns>
    /// <remarks>
    /// Preconditions:
    /// The email and password in the request must not be null or empty.
    /// Postconditions:
    /// If the email and password are valid, then login is successful and a LoginResponse is returned.
    /// Otherwise, the login attempt fails and an unauthorized response is returned.
    /// Raises:
    /// An exception is thrown if the email doesn't exist in the database.
    /// </remarks>
    [HttpPost("login")]
    public async Task<ActionResult> LoginUser(
        [FromBody] LoginRequest request)
    {
        try
        {   
            
            var user = await _authService.ValidateUserAsync(request);
            var claims = new List<Claim> {
                new (ClaimTypes.NameIdentifier, user.Id.ToString()),
                new (ClaimTypes.Email, user.Email),
                new (ClaimTypes.Name, user.DisplayName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var timeProvider = HttpContext.RequestServices.GetRequiredService<TimeProvider>();

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = timeProvider.GetUtcNow().AddDays(7)
                }
            );

            await _verificationCodeService.CancelPasswordResetAsync(request.Email);

            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return Unauthorized(ex.Message);
        }

    }

    /// <summary>
    /// Handles logout by removing authentication cookie.
    /// There is no failing condition since no matter what happens the front end doesn't need to know
    /// </summary>
    /// <returns>
    /// HTTP OK response
    /// </returns>
    [HttpPost("logout")]
    public async Task<ActionResult> LogoutUser()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    /// <summary>
    /// Handles forgot password requests by sending a password reset email if the email exists in the database.
    /// </summary>
    /// <param name="request">
    /// The request containing the user's email.
    /// </param>
    /// <param name="mailKitService">
    /// The service used to send emails.
    /// </param>
    /// <returns>
    /// HTTP OK response if the email is sent successfully or if the email doesn't exist.
    /// </returns>
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            await _verificationCodeService.GenerateAndStoreCodeAsync(request.Email, CodeType.PasswordReset);
        }
        catch (ArgumentException)
        {
        }

        return Ok();
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var valid = HttpContext.Session.GetString(CodeType.PasswordReset.ToString());
            var email = HttpContext.Session.GetString("PendingResetEmail");
            var expiryString = HttpContext.Session.GetString("ResetExpiry");
            if (valid != "true" || email == null || expiryString == null)
            {
                return UnprocessableEntity("No current validation code.");
            }
            var expiry = DateTimeOffset.Parse(expiryString);


            if (DateTimeOffset.UtcNow > expiry)
            {
                HttpContext.Session.Remove(CodeType.PasswordReset.ToString());
                HttpContext.Session.Remove("PendingResetEmail");
                HttpContext.Session.Remove("ResetExpiry");

                return UnprocessableEntity(new
                {
                    message = "Code is no longer valid, please ask for a new code."
                });
            }
            await _authService.ResetPasswordAsync(email, request.NewPassword);

            await _emailService.SendEmailAsync(email, "Password reset successful",
                "Hello, \n\nYour password has been successfully reset.\n\nIf you did not request this change, please contact support immediately.");

            HttpContext.Session.Remove(CodeType.PasswordReset.ToString());
            HttpContext.Session.Remove("PendingResetEmail");
            return Ok();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    /// <summary>
    /// Handles cancel reset requests
    /// </summary>
    /// <param name="request">
    /// The request containing the user's email
    /// </param>
    /// <returns></returns>
    [HttpPost("cancel-reset")]
    public async Task<ActionResult> CancelReset([FromBody] ForgotPasswordRequest request)
    {
        HttpContext.Session.Remove(CodeType.PasswordReset.ToString());
        HttpContext.Session.Remove("PendingResetEmail");
        await _verificationCodeService.CancelPasswordResetAsync(request.Email);
        return Ok();
    }

    /// <summary>
    /// Checks if a user with a corresponding email exists
    /// </summary>
    /// <returns>
    /// HTTP OK response if no user exists
    /// HTTP Conflict if a user does exist
    /// HTTP Bad Request if the input is malformed
    /// </returns>
    [HttpGet("check-email")]
    public async Task<ActionResult> CheckEmailExists([FromQuery] string email)
    {
        if (await _authService.GetUserByEmailAsync(email) != null)
        {
            return Conflict("Email already exists");
        }
        else
        {
            return Ok();
        }
    }
}
