using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using NSubstitute;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;

namespace Grinder.Api.Tests.Services;

public class VerificationCodeServiceTests
{
    private VerificationCodeService CreateService(IEmailService emailService)
    {
        var dbContextFactory = Substitute.For<IDbContextFactory<DatabaseContext>>();
        return new VerificationCodeService(dbContextFactory, TimeProvider.System, emailService);
    }

    [Theory]
    [InlineData(CodeType.EmailVerification, "Verify your email address")]
    [InlineData(CodeType.PasswordReset, "Reset your password")]
    [InlineData(CodeType.PasswordUpdate, "Confirm your password change")]
    [InlineData(CodeType.PasswordResetCancellation, "Password reset cancelled")]
    public void GenerateEmailSubject_KnownTypes_ReturnsExpectedSubject(
        CodeType type, string expectedSubject)
    {
        var subject = VerificationCodeService.GenerateEmailSubject(type);

        Assert.Equal(expectedSubject, subject);
    }

    [Fact]
    public void GenerateEmailSubject_UnknownType_ReturnsFallback()
    {
        var subject = VerificationCodeService.GenerateEmailSubject((CodeType)999);

        Assert.Equal("Your verification code", subject);
    }

    [Theory]
    [InlineData(CodeType.EmailVerification, "123456", "email verification code", "123456", "expire in 5 minutes")]
    [InlineData(CodeType.PasswordReset, "654321", "password reset code", "654321", "expire in 5 minutes")]
    [InlineData(CodeType.PasswordUpdate, "111111", "update password code", "111111", "expire in 5 minutes")]
    public void GenerateEmailBody_CodeTypes_ContainsCodeAndExpiry(CodeType type, string code, string expectedText, string expectedCode, string expectedExpiry)
    {
        var body = VerificationCodeService.GenerateEmailBody(type, code);

        Assert.Contains(expectedCode, body);
        Assert.Contains(expectedText, body);
        Assert.Contains(expectedExpiry, body);
    }

    [Fact]
    public void GenerateEmailBody_PasswordResetCancellation_ContainsCancelledMessage()
    {
        var body = VerificationCodeService.GenerateEmailBody(CodeType.PasswordResetCancellation, "");

        Assert.Contains("Your password reset process has been cancelled. If you did not request this, please contact support immediately.", body);
    }

    [Fact]
    public void GenerateEmailBody_UnknownType_ContainsCodeAndFallbackText()
    {
        var body = VerificationCodeService.GenerateEmailBody((CodeType)999, "999999");

        Assert.Contains("Your verification code is:", body);
        Assert.Contains("999999", body);
        Assert.Contains("This code will expire in 5 minutes.", body);
    }

    [Theory]
    [InlineData(CodeType.EmailVerification, 2, "expire in 2 minutes")]
    [InlineData(CodeType.PasswordReset, 2, "expire in 2 minutes")]
    [InlineData(CodeType.PasswordUpdate, 2, "expire in 2 minutes")]
    [InlineData(CodeType.EmailVerification, null, "expire in 5 minutes")]
    [InlineData(CodeType.PasswordReset, null, "expire in 5 minutes")]
    [InlineData(CodeType.PasswordUpdate, null, "expire in 5 minutes")]
    public void GenerateEmailBody_CodeTypes_ContainsCorrectMinutesRemaining(CodeType type, int? expiryMinutes, string expiryString)
    {
        var body = string.Empty;
        if (expiryMinutes == null)
        {
            body = VerificationCodeService.GenerateEmailBody(type, "123456");
        }
        else
        {
            body = VerificationCodeService.GenerateEmailBody(type, "123456", (int)expiryMinutes);
        }

        Assert.Contains(expiryString, body);
    }

    [Fact]
    public void GenerateSixDigitCode_ReturnsExactlySixDigits()
    {
        var code = VerificationCodeService.GenerateSixDigitCode();

        Assert.Equal(6, code.Length);
    }

    [Fact]
    public void GenerateSixDigitCode_ReturnsOnlyNumericCharacters()
    {
        var code = VerificationCodeService.GenerateSixDigitCode();

        Assert.True(code.All(char.IsDigit));
    }

    [Theory]
    [InlineData(2, "expire in 2 minutes")]
    [InlineData(4, "expire in 4 minutes")]
    [InlineData(6, "expire in 6 minutes")]
    public async Task SendEmail_WithExpiry_SendsCorrectMinutesRemaining(int expiryMinutes, string expiryString)
    {
        var emailService = Substitute.For<IEmailService>();
        var service = CreateService(emailService);
        var expiry = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        await service.SendEmail("test@example.com", CodeType.EmailVerification, "123456", expiry);

        await emailService.Received(1).SendEmailAsync(
            "test@example.com",
            "Verify your email address",
            Arg.Is<string>(body => body.Contains("123456") && body.Contains(expiryString)));
    }

    [Fact]
    public async Task SendEmail_SendsToCorrectEmail()
    {
        var emailService = Substitute.For<IEmailService>();
        var service = CreateService(emailService);

        await service.SendEmail("specific@example.com", CodeType.EmailVerification, "123456");

        await emailService.Received(1).SendEmailAsync(
            "specific@example.com",
            Arg.Any<string>(),
            Arg.Any<string>());
    }
}
