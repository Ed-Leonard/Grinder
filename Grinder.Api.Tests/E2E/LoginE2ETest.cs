using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Microsoft.Playwright;
using Grinder.Api.Models.DTOs;
using Shouldly;

namespace Grinder.Api.Tests.E2E;

public class LoginE2ETest : E2ETestFixture
{
    public LoginE2ETest(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task ShouldLoginSuccessfully()
    {
        // Act: Navigate to login page and fill in the form
        await Page.GotoAsync("http://localhost/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in the login form
        await Page.Locator("#floatingEmail").FillAsync("jane@example.com");
        await Page.Locator("#password").FillAsync("P4$$word");

        // Click the login button
        await Page.Locator("button[type='submit']").ClickAsync();

        // If we wanted to wait for navigation to complete to a specific page...
        await Page.WaitForURLAsync("**/profile/*");

        // You may instead want to look for elements on the page with something like
        // await Page.Locator("h1:has-text('Books')").WaitForAsync();

        // Assert: Verify we're on jane's own profile page (logged in)
        var currentUrl = Page.Url;
        currentUrl.ShouldStartWith("http://localhost/profile/");
    }

    [Fact]
    public async Task ShouldShowErrorWithInvalidCredentials()
    {
        // Act: Navigate to login page
        await Page.GotoAsync("http://localhost/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in invalid credentials
        await Page.Locator("#floatingEmail").FillAsync("invalid@example.com");
        await Page.Locator("#password").FillAsync("WrongPassword123");

        // Click the login button
        await Page.Locator("button[type='submit']").ClickAsync();

        // Wait for error message to appear
        await Page.WaitForSelectorAsync(".input-invalid");
        var errors = Page.Locator(".input-invalid");
        var allText = await errors.AllTextContentsAsync();

        // Assert: exactly 2 error messages shown
        allText.Count.ShouldBe(2);

        // Assert: both contain the expected text
        allText.ShouldAllBe(text => text.Contains("Invalid email or password"));
    }
}

// Helper record for CSRF token response
file record CsrfTokenResponse(string Token);

