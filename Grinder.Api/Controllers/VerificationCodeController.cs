using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Controllers;

/// <summary>
/// Handles verification code related HTTP requests.
/// </summary>
/// <remarks>
/// This controller gives verification related logic to the verification code service and returns HTTP responses based on the results.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class VerificationCodeController : ControllerBase
{
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    /// <summary>
    /// Creates a new instance of the VerificationCodeController class.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Creates a database context to access the database.
    /// </param>
    /// <param name="verificationCodeService">
    /// The service handling verification code related business logic.
    /// </param>
    public VerificationCodeController(IDbContextFactory<DatabaseContext> dbContextFactory, IVerificationCodeService verificationCodeService)
    {
        _verificationCodeService = verificationCodeService;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Verifies a verification code for a user.
    /// </summary>
    /// <param name="request">
    /// Contains the user's email, code, and code type.
    /// </param>
    /// <returns>
    /// 200 OK if the code is valid,
    /// 400 Bad Request if the code is invalid or expired,
    /// </returns>
    [HttpPost("verify")]
    public async Task<ActionResult> VerifyCode([FromBody] VerifyCodeRequest request)
    {
        try
        {
            var expiry = await _verificationCodeService.VerifyCodeAsync(request.Email, request.Code, request.Type);
            if (request.Type != CodeType.EmailVerification)
            {
                HttpContext.Session.SetString(request.Type.ToString(), "true");
            }
            if (request.Type == CodeType.PasswordReset)
            {
                HttpContext.Session.SetString("PendingResetEmail", request.Email);
                HttpContext.Session.SetString("ResetExpiry", expiry.ToString("o"));
            } else if (request.Type == CodeType.PasswordUpdate)
            {
                HttpContext.Session.SetString("UpdateExpiry", expiry.ToString("o"));
            }
            return Ok();
        }
        catch (ArgumentException ex)
        {
            if (ex.Message == "Code is no longer valid, please ask for a new code.")
            {
                return UnprocessableEntity(new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generates and stores a new verification code for a user.
    /// </summary>
    /// <param name="request">
    /// The request containing the email of the user and the type of verification code to generate.
    /// </param>
    /// <returns>
    /// An HTTP response indicating whether the verification code was successfully generated.
    /// </returns>
    [HttpPost("generate")]
    public async Task<ActionResult> GenerateCode([FromBody] GenerateVerificationCodeRequest request)
    {
        try
        {
            await _verificationCodeService.GenerateAndStoreCodeAsync(request.Email, request.Type);
            return Ok();
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Checks if user is verified for a specific action
    /// </summary>
    /// <param name="codeType">
    /// The query which contains the type of code user is checking they are verified with
    /// </param>
    /// <returns>
    ///	HTTP Ok if they are verified
    ///	HTTP Unauthorized if they are not
    /// </returns>
    [HttpGet("verified")]
    public async Task<ActionResult> VerificationStatus([FromQuery] CodeType codeType)
    {
        var verified = HttpContext.Session.GetString(codeType.ToString());
        if (verified == "true")
        {
            return Ok();
        }
        else
        {
            return Unauthorized();
        }
    }

}
