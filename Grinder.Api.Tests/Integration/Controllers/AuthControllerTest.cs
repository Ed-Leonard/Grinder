using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class AuthControllerTest : BaseIntegrationTestFixture
{

    public AuthControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true)
    {
    }

    protected override void ConfigureCustomServicesForTesting(IServiceCollection services)
    {
        services.RemoveAll<IEmailService>();
        var emailServiceMock = NSubstitute.Substitute.For<IEmailService>();
        emailServiceMock
            .SendEmailAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<string>())
            .Returns(Task.CompletedTask);
        services.AddSingleton(emailServiceMock);
    }

    private User _user = null!;

    private async Task RegisterAndLogin()
    {
        var existingUser = new User
        {
            Email = "test@test.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, "Password123!");

        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var loginUserRequest = new LoginRequest("test@test.com", "Password123!");

        var loginResponse = await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    [Fact]
    public async Task LoginUser_ValidDetails_ReturnsOk()
    {
        var user = new User
        {
            Email = "test@ex.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };
        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");

        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var loginRequest = new LoginRequest("test@ex.com", "Password123!");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "Password123!")]
    [InlineData("", "Password123!")]
    public async Task LoginUser_EmptyEmail_ReturnsBadRequest(string? email, string password)
    {
        var loginRequest = new LoginRequest(email, password);

        var response = await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }


    [Theory]
    [InlineData("test@ex.com", null)]
    [InlineData("test@ex.com", "")]
    public async Task LoginUser_EmptyPassword_ReturnsBadRequest(string email, string? password)
    {
        var loginRequest = new LoginRequest(email, password);
        var response = await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginUser_UnverifiedAccount_ReturnsForbidden()
    {
        var user = new User
        {
            Email = "test@ex.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = false
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");

        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var loginRequest = new LoginRequest("test@ex.com", "Password123!");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var message = await response.Content.ReadAsStringAsync();
        message.ShouldContain("Account is not validated yet, check your emails.");
    }

    [Fact]
    public async Task RegisterUser_ValidDetails_ReturnOkStatusCode()
    {
        var registerUserRequest = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("aa")]
    [InlineData("123")]
    [InlineData("aa1")]
    [InlineData("aa!")]
    [InlineData("a ")]
    [InlineData("")]
    [InlineData("          a         ")]
    [InlineData("          ")]
    [InlineData("//)))")]
    public async Task RegisterUser_InvalidName_ReturnBadRequest(string name)
    {
        var registerUserRequest = new RegisterRequest("test@example.email", name, "Password123!", "NZ");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("invalidemail")]
    [InlineData("invalid@email")]
    [InlineData("in..valid@email.com")]
    [InlineData(".invalid@email.com")]
    [InlineData("invalid@email.123")]
    [InlineData("")]
    public async Task RegisterUser_InvalidEmail_ReturnBadRequest(string email)
    {
        var registerUserRequest = new RegisterRequest(email, "name", "Password123!", "NZ");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("", "name", "Password123!", "NZ")]
    [InlineData("valid@email.com", "", "Password123!", "NZ")]
    [InlineData("valid@email.com", "name", "", "NZ")]
    [InlineData("valid@email.com", "name", "Password123!", "")]
    public async Task RegisterUser_MissingRequiredField_ReturnBadRequest(string email, string name, string password, string country)
    {
        var registerUserRequest = new RegisterRequest(email, name, password, country);

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUser_DuplicateEmail_ReturnConflict()
    {
        var existingUser = new User
        {
            Email = "test@ex.com",
            DisplayName = "Test User",
            PasswordHash = "23d!",
            Country = "NZ"
        };

        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var registerUserRequest = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LogoutUser_NoToken_ReturnOk()
    {
        var existingUser = new User
        {
            Email = "test@ex.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var logoutResponse = await HttpClient.PostAsJsonAsync("api/auth/logout", "");
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LogoutUser_ValidToken_ReturnOk()
    {
        var existingUser = new User
        {
            Email = "test@ex.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, "Password123!");

        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var loginUserRequest = new LoginRequest("test@ex.com", "Password123!");

        var loginResponse = await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
        loginResponse.Headers.Contains("Set-Cookie").ShouldBeTrue();
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var logoutResponse = await HttpClient.PostAsJsonAsync("api/auth/logout", "");
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CheckEmail_EmailExists_ReturnConflict()
    {
        var registerUserRequest = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var emailExists = await HttpClient.GetAsync("/api/auth/check-email?email=test@ex.com");
        emailExists.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CheckEmail_EmailDoesNotExists_ReturnOk()
    {
        var emailExists = await HttpClient.GetAsync("/api/auth/check-email?email=test@ex.com");
        emailExists.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_EmailExists_ReturnsOk()
    {
        var registerUserRequest = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var forgotPasswordResponse = await HttpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email = "test@ex.com" });
        forgotPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_EmailDoesNotExist_ReturnsOk()
    {
        var forgotPasswordResponse = await HttpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nonexistent@ex.com" });
        forgotPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_InvalidEmail_ReturnsBadRequest()
    {
        var forgotPasswordResponse = await HttpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email = "invalid-email" });
        forgotPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_ValidCodeAndPassword_ReturnsOk()
    {
        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Type = CodeType.PasswordReset,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        var verifyRequest = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordReset);
        await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyRequest);

        var resetRequest = new ResetPasswordRequest("CoolerPassword123$");
        var response = await HttpClient.PostAsJsonAsync("/api/auth/reset-password", resetRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }


    [Fact]
    public async Task ResetPassword_NotVerified_ReturnsUnprocessableEntity()
    {
        var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            Type = CodeType.PasswordReset,
            UserId = _user.Id
        });
        await dbContext.SaveChangesAsync();

        var resetRequest = new ResetPasswordRequest("CoolerPassword123$");
        var response = await HttpClient.PostAsJsonAsync("/api/auth/reset-password", resetRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ResetPassword_ExpiredCode_ReturnsUnproccessableEntity()
    {
        // super flaky test, relies on delaying the request to update password so that 
        // code verification works -> waits -> update password doesnt work since context expiry string is in the past
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();

        dbContext.VerificationCodes.Add(new VerificationCode
        {
            Code = "123456",
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(2),
            Type = CodeType.PasswordReset,
            UserId = _user.Id
        }); 
        await dbContext.SaveChangesAsync();

        VerifyCodeRequest verifyCode = new VerifyCodeRequest(_user.Email, "123456", CodeType.PasswordReset);
        await HttpClient.PostAsJsonAsync("/api/verificationcode/verify", verifyCode);

        await Task.Delay(3000);

        var ResetPasswordRequest = new ResetPasswordRequest("NewPassword123!");

        var response = await HttpClient.PostAsJsonAsync("/api/auth/reset-password", ResetPasswordRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Login_WithPendingResetCode_DeletesCodeAndReturnsOk()
    {
        var registerRequest = new RegisterRequest("test@cool.com", "Cool fella", "Password6767$$", "NZ");
        await HttpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        await VerifyUser("test@cool.com");

        await HttpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email = "test@cool.com" });

        var loginResponse = await HttpClient.PostAsJsonAsync("api/auth/login",
            new { email = "test@cool.com", password = "Password6767$$" });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var resetCode = await context.VerificationCodes
            .FirstOrDefaultAsync(vc => vc.Type == CodeType.PasswordReset
                && context.Users.Any(u => u.Email == "test@cool.com" && u.Id == vc.UserId));
        resetCode.ShouldBeNull();
    }

    [Fact]
    public async Task CancelReset_WithPendingResetCode_DeletesCode()
    {
        var registerRequest = new RegisterRequest("test@cool.com", "Cool fella", "Password6767$$", "NZ");
        await HttpClient.PostAsJsonAsync("api/auth/register", registerRequest);
        await VerifyUser("test@cool.com");

        await HttpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email = "test@cool.com" });

        var cancelResponse = await HttpClient.PostAsJsonAsync("/api/auth/cancel-reset",
            new { email = "test@cool.com" });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var resetCode = await context.VerificationCodes
            .FirstOrDefaultAsync(vc => vc.Type == CodeType.PasswordReset
                && context.Users.Any(u => u.Email == "test@cool.com" && u.Id == vc.UserId));

        resetCode.ShouldBeNull();
    }

    [Fact]
    public async Task CancelReset_UnknownEmail_ReturnsOk()
    {
        var cancelResponse = await HttpClient.PostAsJsonAsync("api/auth/cancel-reset",
            new { email = "notanemail@email.com" });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }



    private async Task VerifyUser(string email)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = await context.Users.FirstAsync(u => u.Email == email);
        user.IsVerified = true;
        await context.SaveChangesAsync();
    }
}
