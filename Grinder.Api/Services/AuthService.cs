using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace Grinder.Api.Services;

public record LoginResult(string AccessToken);

/// <summary>
/// Defines user-related business logic.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Determines if a login response should be returned based on a login request.
    /// </summary>
    /// <param name="request">
    /// The login request containing the email and passsword used for the login attempt.
    /// </param>
    /// <returns>
    /// A LoginResponse if the login attempt is succesful.
    /// Null if the login attempt fails due to an incorrect password.
    /// </returns>
    /// <remarks>
    /// Preconditions:
    /// The email and password in the request must be non-null and non-empty.
    /// Postconditions:
    /// If the email and password are valid, a LoginResponse containg the user's ID, email, display name.
    /// Otherwise, null is returned.
    /// Raises:
    /// An exception is thrown if the email doesn't exist in the database.
    /// </remarks>
    Task<User> ValidateUserAsync(LoginRequest request);
    Task RegisterAsync(RegisterRequest request);
    Task ResetPasswordAsync(string email, string newPassword);
    Task<User?> GetUserByEmailAsync(string email);
}

/// <summary>
/// Defines user-related business logic, including validation and authentication of login attempts.
/// </summary>
/// <remarks>
/// Verifies credentials
/// This service does not perform password hashing, but relies on hashed passwords stored in the database.
/// </remark>
public class AuthService : IAuthService
{

    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IPasswordHasher<User> _passwordHasher;

    private readonly IVerificationCodeService _verificationCodeService;

    /// <summary>
    /// Initializes a new instance of the AuthService class.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Used to create a database contexts to access database.
    /// </param>
    /// <param name="timeProvider">
    /// Provides a mock time for testing.
    /// </param>
    public AuthService(IDbContextFactory<DatabaseContext> dbContextFactory, TimeProvider timeProvider, IVerificationCodeService verificationCodeService, IPasswordHasher<User> passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _verificationCodeService = verificationCodeService;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Verifies user's credentials, hashes the password for comparison with the stored hash and returns the user if valid
    /// </summary>
    /// <remarks>
    /// Implementation details:
    /// Retrieves the user from the database using the email provided in the login request.
    /// Hashes the provided password and compares it with the stored password hash using PasswordHasher.
    /// Returns the user object
    /// <exception cref="Exception">
    /// Thrown if the email is not found in the database.
    /// </exception>
    public async Task<User> ValidateUserAsync(LoginRequest request)
    {
        if (!ValidationHelper.ValidateEmail(request.Email))
        {
            throw new ArgumentException("Invalid email address. Email must be in the format 'jane@doe.nz'");
        }

        await using var context = await _dbContextFactory.CreateDbContextAsync();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            throw new Exception("Invalid email or password");
        }

        if (user.IsVerified == false)
        {
            throw new InvalidOperationException("Account is not validated yet, check your emails.");
        }
        
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            throw new Exception("Invalid email or password");
        }

        return user;
    }

    /// <summary>
    /// Finds the user object in database associated with an email
    /// </summary>
    /// <param name="email">
    /// the users email to search database for
    /// </param>
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Verifies that details are valid and then creates new user in database
    /// </summary>
    /// <remarks>
    /// Implementation details:
    /// Hashes the provided password using PasswordHasher and stores that in db.
    /// <exception cref="Exception">
    /// Thrown if the email is not found in the database.
    /// </exception>
    public async Task RegisterAsync(RegisterRequest request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var email = request.Email.ToLowerInvariant();
        var existingUser = await GetUserByEmailAsync(email);
        var displayName = request.DisplayName.Trim();

        if (await context.Users.AnyAsync(u => u.Email == email))
        {

            throw new InvalidOperationException("A user with this email already exists.");
        }
        var expiredUser = await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Email == email);
        if (expiredUser != null)
        {
            context.Users.Remove(expiredUser);
            await context.SaveChangesAsync();
        }

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            Country = request.Country,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        if (ValidationHelper.ValidateEntity(user))
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
            try
            {
                await _verificationCodeService.GenerateAndStoreCodeAsync(email, CodeType.EmailVerification);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send verification email: {ex.Message}");
            }
        }
        else
        {
            throw new ArgumentException("Invalid details");
        }
    }

    public async Task ResetPasswordAsync(string email, string newPassword)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            throw new ArgumentException("User does not exist");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await context.SaveChangesAsync();
    }
}
