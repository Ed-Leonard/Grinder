using Grinder.Api.Models.DTOs;
using Grinder.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;

namespace Grinder.Api.Tests.Integration.Services;

public class AuthServiceTests : BaseIntegrationTestFixture
{

    private IAuthService ServiceUnderTest => ServiceProvider.GetRequiredService<IAuthService>();

    public AuthServiceTests(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsUser()
    {
        var user = new User
        {
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = true
        };

        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = passwordHasher.HashPassword(user, "Password123!");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var loginRequest = new LoginRequest("test@example.com", "Password123!");

        var result = await ServiceUnderTest.ValidateUserAsync(loginRequest);

        result.ShouldNotBeNull();
        result.Email.ShouldBe("test@example.com");
        result.DisplayName.ShouldBe("Test User");
    }

    [Fact]
    public async Task Login_InvalidEmail_ThrowsException()
    {
        var loginRequest = new LoginRequest("invalid@ex.com", "Password123!");

        await Should.ThrowAsync<Exception>(() =>
            ServiceUnderTest.ValidateUserAsync(loginRequest)
        );
    }

    [Fact]
    public async Task Login_InvalidPassword_ThrowsException()
    {
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "Password1",
            Country = "NZ",
            IsVerified = true
        };

        var loginRequest = new LoginRequest("test@example.com", "InvalidPassword!");
        await Should.ThrowAsync<Exception>(() =>
            ServiceUnderTest.ValidateUserAsync(loginRequest)
        );
    }

    [Fact]
    public async Task Login_UnverifiedUser_ThrowsInvalidOperationException()
    {
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "Password123!",
            Country = "NZ",
            IsVerified = false
        };

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        var loginRequest = new LoginRequest("test@example.com", "Password123!");

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            ServiceUnderTest.ValidateUserAsync(loginRequest)
        );

        ex.Message.ShouldBe("Account is not validated yet, check your emails.");
    }

    [Fact]
    public async Task Register_ValidDetails_UserInDatabase()
    {
        var request = new RegisterRequest("test@ex.com", "Test User", "Password123!", "NZ");

        await ServiceUnderTest.RegisterAsync(request);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var singleUserInDb = context.Users.ShouldHaveSingleItem();
        singleUserInDb.Id.ShouldBe(1);
        singleUserInDb.Email.ShouldBe("test@ex.com");
        singleUserInDb.DisplayName.ShouldBe("Test User");
        singleUserInDb.Country.ShouldBe("NZ");

        singleUserInDb.PasswordHash.ShouldNotBe("Password123!");
        singleUserInDb.PasswordHash.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("test@ex.com", "", "Password123!", "NZ")]
    [InlineData("", "Test User", "Password123!", "NZ")]
    [InlineData("test@ex.com", "Test User", "Password123!", "")]
    public async Task Register_InvalidDetails_ThrowsException(
     string email,
     string displayName,
     string password,
     string country)
    {
        var request = new RegisterRequest(email, displayName, password, country);
        await Should.ThrowAsync<ArgumentException>(() => ServiceUnderTest.RegisterAsync(request));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserByEmail_ValidEmail_ReturnUser()
    {
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "Password1",
            Country = "NZ"
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var foundUser = await ServiceUnderTest.GetUserByEmailAsync("test@example.com");

        foundUser.ShouldBeEquivalentTo(user);
    }

    [Fact]
    public async Task GetUserByEmail_InvalidEmail_ReturnNull()
    {
        var user = new User
        {
            Id = 1,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "Password1",
            Country = "NZ"
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var foundUser = await ServiceUnderTest.GetUserByEmailAsync("");

        foundUser.ShouldBeNull();
    }

    [Fact]
    public async Task ResetPassword_ValidEmail_UpdatesPasswordHash()
    {
        var user = new User
        {
            Email = "test@cool.com",
            DisplayName = "Cool User",
            PasswordHash = "Password6767$$",
            Country = "NZ",
            IsVerified = true
        };
        var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.PasswordHash = passwordHasher.HashPassword(user, "Password676$$");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        await ServiceUnderTest.ResetPasswordAsync("test@cool.com", "Password676767$$");
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var updatedUser = await dbContext.Users.FirstAsync(u => u.Email == "test@cool.com");
        var result = passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.PasswordHash, "Password676767$$");
        result.ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task ResetPassword_NonExistantEmail_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() =>
            ServiceUnderTest.ResetPasswordAsync(
                "idontexist@gmail.com",
                "Newpassword123"
            )
        );
    }
    
}
