using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grinder.Api.Tests.Integration.Controllers;

public class SearchControllerTest : BaseIntegrationTestFixture
{
    public SearchControllerTest(WebApplicationFactory<Program> webAppFactory) : base(webAppFactory, handleCookies: true) { }

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

        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(existingUser);
        await dbContext.SaveChangesAsync();

        var loginUserRequest = new LoginRequest("test@test.com", "Password123!");

        var loginResponse = await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
    }

    // Runs before every test so we don't need to call RegisterAndLogin every test
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();

        await using var context = await DbContextFactory.CreateDbContextAsync();
        _user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
    }

    [Fact]
    public async Task GetSearchResults_ReturnsOk()
    {
        var response = await HttpClient.GetAsync("/api/search?query=test");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSearchResults_ReturnsEmpty_WhenQueryDoesNotMatch()
    {
        var response =
            await HttpClient.GetAsync(
                "/api/search?query=NonExistentQuery");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result =
            await response.Content.ReadFromJsonAsync<SearchResponseDto>();

        result.ShouldNotBeNull();
        result.Users.ShouldBeEmpty();
        result.Tasks.ShouldBeEmpty();
    }
}
