using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Grinder.Api.Models.Attachments;

namespace Grinder.Api.Services;

/// <summary>
/// Defines user-related business logic.
/// </summary>
public interface IUserService
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
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> EditProfileAsync(int id, UpdateProfileRequest request);
    Task<User?> GetUserByIdAsync(int id);
    Task UpdatePasswordAsync(int userId, string newPassword, string oldPassword, string confirmPassword);
    Task<User?> UploadProfilePictureAsync(int userId, IFormFile file, CancellationToken cancellationToken = default);
    Task<StoredFile?> GetProfilePictureUrlAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines user-related business logic, including validation and authentication of login attempts.
/// </summary>
/// <remarks>
/// Verifies credentials
/// This service does not perform password hashing, but relies on hashed passwords stored in the database.
/// </remark>
public class UserService : IUserService
{

    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    private readonly IAttachmentValidator _attachmentValidator;
    private readonly IPasswordHasher<User> _passwordHasher;

    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IAttachmentService _attachmentService;

    /// <summary>
    /// Initializes a new instance of the UserService class.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Used to create a database contexts to access database.
    /// </param>
    /// <param name="timeProvider">
    /// Provides a mock time for testing.
    /// </param>
    public UserService(
        IDbContextFactory<DatabaseContext> dbContextFactory,
        TimeProvider timeProvider,
        IVerificationCodeService verificationCodeService,
        IAttachmentService attachmentService,
        IPasswordHasher<User> passwordHasher)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _verificationCodeService = verificationCodeService;
        _attachmentService = attachmentService;
        _passwordHasher = passwordHasher;
    }

    /// Finds the user object in database associated with an id
    /// </summary>
    /// <param name="id">
    /// the users id to search database for
    /// </param>
    public async Task<User?> GetUserByIdAsync(int id)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();
        return await context.Users
            .Include(u => u.SelectedBadge)
            .FirstOrDefaultAsync(u => u.Id == id);
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
    /// Updates the profile of a user identified by their email address with the provided profile information.
    /// </summary>
    /// <param name="id">
    /// The id of the user whose profile to update.
    /// </param>
    /// <param name="request">
    /// The request containing the updated profile information.
    /// </param>
    /// <returns>
    /// The updated user if successful, otherwise null.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the new email address is already in use by another account.
    /// </exception>
    public async Task<User?> EditProfileAsync(int id, UpdateProfileRequest request)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return null;
        }

        var email = request.Email.ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        var dupeUser = await GetUserByEmailAsync(email);

        if (dupeUser != null && dupeUser.Id != id)
        {
            throw new InvalidOperationException("This email address is already in use by another account.");
        }
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-5);
        var expiredUser = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email && u.Id != id && !u.IsVerified && u.CreatedAt <= cutoff);
        if (expiredUser != null)
        {
            context.Users.Remove(expiredUser);
            await context.SaveChangesAsync();
        }

        user.Email = email;
        user.DisplayName = displayName;
        user.Country = request.Country;

        if (ValidationHelper.ValidateEntity(user))
        {
            await context.SaveChangesAsync();
        }
        else
        {
            return null;
        }

        return user;
    }

    /// <summary>
    /// Updates the password of a user identified by their ID.
    /// </summary>
    /// <param name="userId">The ID of the user whose password to update.</param>
    /// <param name="newPassword">The new password to set.</param>
    /// <param name="oldPassword">The current password to verify before allowing the update.</param>
    /// <param name="confirmPassword">The confirmation of the new password to ensure it matches.</param>
    /// <exception cref="KeyNotFoundException">Thrown if no user with the given ID exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the old password does not match the stored password.</exception>
    /// <exception cref="ArgumentException">Thrown if the new password does not meet complexity requirements or passwords dont match.</exception>
    public async Task UpdatePasswordAsync(int userId, string newPassword, string oldPassword, string confirmPassword)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new KeyNotFoundException("User not found.");
        
        var verifyOldPassword = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, oldPassword);

        if (verifyOldPassword == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Old password does not match password on file");
        }

        if (newPassword != confirmPassword)
        {
            throw new ArgumentException("Passwords do not match");
        }

        if (!ValidationHelper.ValidatePassword(newPassword))
        {
            throw new ArgumentException("Password must be at least 8 characters long including at least one of each uppercase, lowercase, numbers and special characters.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

        context.Users.Update(user);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Uploads a profile picture for a user, validates the file, saves it to disk, and updates the user's profile picture URL.
    /// </summary>
    /// <param name="userId">The ID of the user uploading the profile picture.</param>
    /// <param name="file">The image file to upload.</param>
    /// <param name="webRootPath">The web root path used to determine the file storage location.</param>
    /// <returns>The updated user if successful, or null if the user was not found.</returns>
    /// <exception cref="AttachmentValidationException">
    /// Thrown if the file type is not one of .jpeg, .png, .svg, or .gif, or if the file exceeds the 5MB size limit.
    /// </exception>
    public async Task<User?> UploadProfilePictureAsync(int userId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        User? user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            return null;
        }

        string profilePictureUrl = await _attachmentService.UploadProfilePictureAsync(file, cancellationToken);

        user.ProfilePictureUrl = profilePictureUrl;

        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<StoredFile?> GetProfilePictureUrlAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using DatabaseContext context =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        string? storedUrl = await context
            .Users.Where(user => user.Id == userId)
            .Select(user => user.ProfilePictureUrl)
            .SingleOrDefaultAsync(cancellationToken);

        return storedUrl is null ? null : _attachmentService.ResolveProfilePicture(storedUrl);
    }
}
