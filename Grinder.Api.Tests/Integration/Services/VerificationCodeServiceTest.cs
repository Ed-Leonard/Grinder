using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;
using NSubstitute;

namespace Grinder.Api.Tests.Integration.Services;

public class VerificationCodeServiceTest : BaseIntegrationTestFixture
{
	private IEmailService EmailServiceMock { get; set; } = null!;
	private IVerificationCodeService ServiceUnderTest => ServiceProvider.GetRequiredService<IVerificationCodeService>();

	public VerificationCodeServiceTest(WebApplicationFactory<Program> webAppFactory)
		: base(webAppFactory) { }
	protected override void ConfigureCustomServicesForTesting(IServiceCollection services)
	{
		services.RemoveAll<IEmailService>();

		EmailServiceMock = Substitute.For<IEmailService>();
		EmailServiceMock
			.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
			.Returns(Task.CompletedTask);

		services.AddSingleton(EmailServiceMock);
	}
	private async Task<User> AddUserToDatabaseAsync(string email = "test@example.com", bool isVerified = false)
	{
		await using var context = await DbContextFactory.CreateDbContextAsync();

		var user = new User
		{
			Email = email,
			DisplayName = "Test User",
			PasswordHash = "not-a-real-hash",
			Country = "NZ",
			CreatedAt = TestNow.UtcDateTime,
			IsVerified = isVerified
		};

		context.Users.Add(user);
		await context.SaveChangesAsync();

		return user;
	}

	[Theory]
	[InlineData(CodeType.EmailVerification, "Verify your email address", "Your email verification code")]
	[InlineData(CodeType.PasswordReset, "Reset your password", "Your password reset code")]
	[InlineData(CodeType.PasswordUpdate, "Confirm your password change", "Your update password code")]
	public async Task GenerateAndStoreCodeAsync_ValidEmail_SendsEmailWithCorrectDetailsAndCodeStoredInDB(CodeType codeType, string expectedSubject, string expectedBody)
	{
		var user = await AddUserToDatabaseAsync();

		await ServiceUnderTest.GenerateAndStoreCodeAsync(user.Email, codeType);

		await using var context = await DbContextFactory.CreateDbContextAsync();
		var codeInDb = context.VerificationCodes.ShouldHaveSingleItem();
		codeInDb.UserId.ShouldBe(user.Id);
		codeInDb.Type.ShouldBe(codeType);
		codeInDb.ExpiresAt.ShouldBe(TestNow.AddMinutes(5).UtcDateTime);

		await EmailServiceMock.Received(1).SendEmailAsync(
			user.Email,
			Arg.Is<string>(subject => subject.Contains(expectedSubject)),
			Arg.Is<string>(body => body.Contains(expectedBody))
		);
	}

	[Fact]
	public async Task GenerateAndStoreCodeAsync_GeneratesValidCodeFormat()
	{
		var user = await AddUserToDatabaseAsync();

		await ServiceUnderTest.GenerateAndStoreCodeAsync(user.Email, CodeType.EmailVerification);

		await using var context = await DbContextFactory.CreateDbContextAsync();
		var code = context.VerificationCodes.Single();
		code.Code.ShouldMatch(@"^\d{6}$");
	}

	[Fact]
	public async Task GenerateAndStoreCodeAsync_UserDoesNotExist_ThrowsArgumentException()
	{
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
		{
			await ServiceUnderTest.GenerateAndStoreCodeAsync("missing@example.com", CodeType.EmailVerification);
		});

		ex.ParamName.ShouldBe("email");
		ex.Message.ShouldBe($"User does not exist (Parameter '{ex.ParamName}')");
	}

	[Fact]
	public async Task GenerateAndStoreCodeAsync_ExistingEmailVerificationCode_DeletesOldCodeAndStoresNewOne()
	{
		var user = await AddUserToDatabaseAsync();

		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "111111",
				ExpiresAt = TestNow.AddMinutes(5).UtcDateTime,
				Type = CodeType.EmailVerification,
				UserId = user.Id
			});

			await context.SaveChangesAsync();
		}

		await ServiceUnderTest.GenerateAndStoreCodeAsync(user.Email, CodeType.EmailVerification);

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		var emailVerificationCodes = verifyContext.VerificationCodes
			.Where(c => c.UserId == user.Id && c.Type == CodeType.EmailVerification)
			.ToList();

		emailVerificationCodes.Count.ShouldBe(1);

		var remainingCode = emailVerificationCodes.Single();
		remainingCode.Code.ShouldNotBe("111111");
	}

	[Fact]
	public async Task VerifyCodeAsync_MatchingValidCode_ReturnsTrue()
	{
		var user = await AddUserToDatabaseAsync(isVerified: true);

		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "123456",
				ExpiresAt = TestNow.AddMinutes(5).UtcDateTime,
				Type = CodeType.PasswordReset,
				UserId = user.Id
			});

			await context.SaveChangesAsync();
		}

		await ServiceUnderTest.VerifyCodeAsync(user.Email, "123456", CodeType.PasswordReset);

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		verifyContext.VerificationCodes.Count().ShouldBe(0);
	}

	[Fact]
	public async Task VerifyCodeAsync_CodeDoesNotExist_ThrowsArgumentException()
	{
		var user = await AddUserToDatabaseAsync();
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
		{
			await ServiceUnderTest.VerifyCodeAsync(user.Email, "123456", CodeType.EmailVerification);
		});
		ex.Message.ShouldBe("Invalid code");
	}

	[Fact]
	public async Task VerifyCodeAsync_UserDoesNotExist_ThrowsArgumentException()
	{
		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
		{
			await ServiceUnderTest.VerifyCodeAsync("missing@example.com", "123456", CodeType.EmailVerification);
		});

		ex.Message.ShouldBe("Code is no longer valid, account no longer exists");
	}

	[Fact]
	public async Task VerifyCodeAsync_CodeExpired_ThrowsArgumentExceptionAndRemovesCode()
	{
		var user = await AddUserToDatabaseAsync(isVerified: true);
		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "123456",
				ExpiresAt = TestNow.AddMinutes(-1).UtcDateTime,
				Type = CodeType.PasswordReset,
				UserId = user.Id
			});
			await context.SaveChangesAsync();
		}

		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
		{
			await ServiceUnderTest.VerifyCodeAsync(user.Email, "123456", CodeType.PasswordReset);
		});
		ex.Message.ShouldBe("Code is no longer valid, please ask for a new code.");

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		verifyContext.VerificationCodes.ShouldBeEmpty();
	}

	[Fact]
	public async Task VerifyCodeAsync_ValidEmailVerificationCode_VerifiesUser()
	{
		var user = await AddUserToDatabaseAsync(isVerified: false);
		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "654321",
				ExpiresAt = TestNow.AddMinutes(5).UtcDateTime,
				Type = CodeType.EmailVerification,
				UserId = user.Id
			});
			await context.SaveChangesAsync();
		}

		await ServiceUnderTest.VerifyCodeAsync(user.Email, "654321", CodeType.EmailVerification);

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		var updatedUser = verifyContext.Users.Single(u => u.Id == user.Id);
		updatedUser.IsVerified.ShouldBeTrue();
		verifyContext.VerificationCodes.ShouldBeEmpty();
	}

	[Fact]
	public async Task VerifyCodeAsync_WrongType_ThrowsArgumentException()
	{
		var user = await AddUserToDatabaseAsync();
		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "123456",
				ExpiresAt = TestNow.AddMinutes(5).UtcDateTime,
				Type = CodeType.PasswordReset,
				UserId = user.Id
			});
			await context.SaveChangesAsync();
		}

		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
		{
			await ServiceUnderTest.VerifyCodeAsync(user.Email, "123456", CodeType.EmailVerification);
		});
		ex.Message.ShouldBe("Invalid code");

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		verifyContext.VerificationCodes.Count().ShouldBe(1);
	}

	[Fact]
	public async Task GenerateAndStoreCodeAsync_ExistingEmailVerificationCode_ReusesOriginalExpiry()
	{
		var user = await AddUserToDatabaseAsync(isVerified: false);
		var originalExpiry = TestNow.AddMinutes(5).UtcDateTime;

		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "111111",
				ExpiresAt = originalExpiry,
				Type = CodeType.EmailVerification,
				UserId = user.Id
			});

			await context.SaveChangesAsync();
		}

		await ServiceUnderTest.GenerateAndStoreCodeAsync(user.Email, CodeType.EmailVerification);

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		var codes = verifyContext.VerificationCodes
			.Where(vc => vc.UserId == user.Id && vc.Type == CodeType.EmailVerification)
			.ToList();

		codes.Count.ShouldBe(1);
		codes.Single().Code.ShouldNotBe("111111");
		codes.Single().ExpiresAt.ShouldBe(originalExpiry);
	}

	[Fact]
	public async Task VerifyCodeAsync_ExpiredEmailVerificationCode_ThrowsArgumentException()
	{
		var user = await AddUserToDatabaseAsync(isVerified: false);

		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "654321",
				ExpiresAt = TestNow.AddMinutes(-1).UtcDateTime,
				Type = CodeType.EmailVerification,
				UserId = user.Id
			});

			await context.SaveChangesAsync();
		}

		var ex = await Should.ThrowAsync<ArgumentException>(async () =>
		{
			await ServiceUnderTest.VerifyCodeAsync(user.Email, "654321", CodeType.EmailVerification);
		});

		ex.Message.ShouldContain("Code is no longer valid, account no longer exists");
	}


	[Fact]
	public async Task CancelPasswordResetAsync_WithPendingCode_DeletesCodeAndSendsEmail()
	{
		var user = await AddUserToDatabaseAsync(isVerified: true);

		await using (var context = await DbContextFactory.CreateDbContextAsync())
		{
			context.VerificationCodes.Add(new VerificationCode
			{
				Code = "676767",
				ExpiresAt = TestNow.AddMinutes(5).UtcDateTime,
				Type = CodeType.PasswordReset,
				UserId = user.Id
			});

			await context.SaveChangesAsync();
		}

		await ServiceUnderTest.CancelPasswordResetAsync(user.Email);

		await using var verifyContext = await DbContextFactory.CreateDbContextAsync();
		verifyContext.VerificationCodes
			.Any(vc => vc.UserId == user.Id && vc.Type == CodeType.PasswordReset)
			.ShouldBeFalse();

		await EmailServiceMock.Received(1).SendEmailAsync(
			user.Email,
			Arg.Is<string>(subject => subject == "Password reset cancelled"),
			Arg.Is<string>(body => body.Contains("password reset process has been cancelled"))
		);
	}
}
