using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class VerificationCodeControllerTest : BaseIntegrationTestFixture
{
    public VerificationCodeControllerTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory, handleCookies: true) { }

    protected override void ConfigureCustomServicesForTesting(IServiceCollection services)
    {
        services.RemoveAll<IEmailService>();
        var emailServiceMock = Substitute.For<IEmailService>();
        emailServiceMock
            .SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.CompletedTask);
        services.AddSingleton(emailServiceMock);
    }

    private async Task<User> AddUserToDatabaseAsync(string email = "test@example.com", bool isVerified = false, bool isExpired = false)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        var user = new User
        {
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = TestNow.UtcDateTime.AddMinutes(isExpired ? -6 : 0),
            IsVerified = isVerified
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return new User
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IsVerified = user.IsVerified
        };
    }

    private async Task AddVerificationCodeToDatabaseAsync(
        int userId,
        string code,
        CodeType type,
        DateTime expiresAt)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();

        context.VerificationCodes.Add(new VerificationCode
        {
            Code = code,
            Type = type,
            ExpiresAt = expiresAt,
            UserId = userId
        });

        await context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(CodeType.EmailVerification)]
    [InlineData(CodeType.PasswordUpdate)]
    [InlineData(CodeType.PasswordReset)]
    [InlineData(CodeType.PasswordResetCancellation)]
    public async Task VerifyCode_ValidCode_ReturnsOk(CodeType codeType)
    {
        var user = await AddUserToDatabaseAsync(isVerified: true);

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "123456",
            codeType,
            TestNow.AddMinutes(5).UtcDateTime);

        var request = new VerifyCodeRequest(user.Email, "123456", codeType);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.VerificationCodes.Count().ShouldBe(0);
    }

    [Theory]
    [InlineData("999999", CodeType.PasswordReset)] // Test wrong code
    [InlineData("123456", CodeType.EmailVerification)] // Test wrong type
    public async Task VerifyCode_InvalidInputs_BadRequestReturned(string code, CodeType type)
    {
        var user = await AddUserToDatabaseAsync();

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "123456",
            CodeType.PasswordReset,
            TestNow.AddMinutes(5).UtcDateTime);

        var request = new VerifyCodeRequest(user.Email, code, type);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().ShouldBe("Invalid code");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.VerificationCodes.Count().ShouldBe(1);
    }

    [Fact]
    public async Task VerifyCode_PasswordResetExpiredCode_UnprocessableEntityReturnedAndCodeRemoved()
    {
        var user = await AddUserToDatabaseAsync(isVerified: true);

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "123456",
            CodeType.PasswordReset,
            TestNow.AddMinutes(-1).UtcDateTime);

        var request = new VerifyCodeRequest(user.Email, "123456", CodeType.PasswordReset);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().ShouldBe("Code is no longer valid, please ask for a new code.");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.VerificationCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyCode_UserDoesNotExist_BadRequestReturned()
    {
        var request = new VerifyCodeRequest("missing@example.com", "123456", CodeType.EmailVerification);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().ShouldBe("Code is no longer valid, account no longer exists");
    }

    [Fact]
    public async Task VerifyCode_ValidEmailVerificationCode_ReturnsOkAndUserVerified()
    {
        var user = await AddUserToDatabaseAsync(isVerified: false);

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "654321",
            CodeType.EmailVerification,
            TestNow.AddMinutes(5).UtcDateTime);

        var request = new VerifyCodeRequest(user.Email, "654321", CodeType.EmailVerification);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var updatedUser = context.Users.Single(u => u.Id == user.Id);
        updatedUser.IsVerified.ShouldBeTrue();
        context.VerificationCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyCode_ExpiredEmailVerificationCode_BadRequestReturned()
    {
        var user = await AddUserToDatabaseAsync(isVerified: false);

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "654321",
            CodeType.EmailVerification,
            TestNow.AddMinutes(-1).UtcDateTime);

        var request = new VerifyCodeRequest(user.Email, "654321", CodeType.EmailVerification);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().ShouldBe("Code is no longer valid, account no longer exists");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.VerificationCodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateCode_ValidEmail_ReturnsOk()
    {
        var user = await AddUserToDatabaseAsync();
        var request = new GenerateVerificationCodeRequest(CodeType.EmailVerification, user.Email);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.VerificationCodes.Count().ShouldBe(1);
    }

    [Fact]
    public async Task GenerateCode_UserDoesNotExist_ReturnsNotFound()
    {
        var request = new GenerateVerificationCodeRequest(CodeType.EmailVerification, "missing@example.com");

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateCode_ResendInvalidatesOldCode_ReturnsOk()
    {
        var user = await AddUserToDatabaseAsync();

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "676767",
            CodeType.EmailVerification,
            TestNow.AddMinutes(5).UtcDateTime);

        var request = new GenerateVerificationCodeRequest(CodeType.EmailVerification, user.Email);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = await DbContextFactory.CreateDbContextAsync();

        var codes = context.VerificationCodes.Where(vc => vc.UserId == user.Id).ToList();
        codes.Count.ShouldBe(1);
        codes.Single().Code.ShouldNotBe("676767");
    }

    [Fact]
    public async Task GenerateCode_ExpiredUnverifiedUser_ReturnsNotFound()
    {
        var user = await AddUserToDatabaseAsync(isExpired: true);

        var request = new GenerateVerificationCodeRequest(CodeType.EmailVerification, user.Email);

        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/generate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Any(u => u.Id == user.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyCode_OldCodeAfterResend_ReturnsBadRequest()
    {
        var user = await AddUserToDatabaseAsync();

        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "676767",
            CodeType.EmailVerification,
            TestNow.AddMinutes(5).UtcDateTime);

        // Resend - generates new code, invalidates old one
        var generateRequest = new GenerateVerificationCodeRequest(CodeType.EmailVerification, user.Email);
        await HttpClient.PostAsJsonAsync("/api/VerificationCode/generate", generateRequest);

        var verifyRequest = new VerifyCodeRequest(user.Email, "676767", CodeType.EmailVerification);
        var response = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", verifyRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("message").GetString().ShouldBe("Invalid code");
    }

    [Theory]
    [InlineData(CodeType.PasswordUpdate)]
    [InlineData(CodeType.PasswordReset)]
    [InlineData(CodeType.PasswordResetCancellation)]
    public async Task VerifyStatus_UserVerified_ReturnsOK(CodeType codeType)
    {
        var user = await AddUserToDatabaseAsync();
        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "123456",
            codeType,
            TestNow.AddMinutes(5).UtcDateTime
        );

        var request = new VerifyCodeRequest(user.Email, "123456", codeType);

        var verifyResponse = await HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", request);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var verifyStatus = await HttpClient.GetAsync($"/api/VerificationCode/verified?codeType={codeType}");
        verifyStatus.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.VerificationCodes.Count().ShouldBe(0);
    }

    [Fact]
    public async Task VerifyStatusOnRegistration_EmailVerification_ReturnsOK()
    {
        var registerUserRequest = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");
        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var verifyStatus = await HttpClient.GetAsync($"/api/VerificationCode/verified?codeType={CodeType.EmailVerification}");
        verifyStatus.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(CodeType.PasswordUpdate)]
    [InlineData(CodeType.PasswordReset)]
    [InlineData(CodeType.PasswordResetCancellation)]
    public async Task VerifyStatusOnRegistration_WrongSessionState_ReturnsUnauthorized(CodeType codeType)
    {
        var registerUserRequest = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");
        var response = await HttpClient.PostAsJsonAsync("/api/auth/register", registerUserRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var verifyStatus = await HttpClient.GetAsync($"/api/VerificationCode/verified?codeType={codeType}");
        verifyStatus.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(CodeType.PasswordUpdate)]
    [InlineData(CodeType.PasswordReset)]
    [InlineData(CodeType.PasswordResetCancellation)]
    public async Task VerifyStatus_UserNotVerified_ReturnsUnauthorized(CodeType codeType) 
    {
        var user = await AddUserToDatabaseAsync();
        await AddVerificationCodeToDatabaseAsync(
            user.Id,
            "123456",
            codeType,
            TestNow.AddMinutes(5).UtcDateTime
        );
        
        var verifyStatus = await HttpClient.GetAsync($"/api/VerificationCode/verified?codeType={codeType}");
        verifyStatus.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
