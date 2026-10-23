// SharedSteps.cs
using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using Grinder.Api.Models.Entities;
using System.Text.Json;
using Shouldly;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class SharedSteps
{
    private readonly AcceptanceTestFixture _fixture;

    public SharedSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I am logged in as a valid user")]
    public async Task GivenIAmLoggedInAsAValidUser()
    {
        await _fixture.LoginAsTestUser();
    }

    [Given(@"there is an account registered with the email {string}")]
    public async Task GivenExistingEmail(string email)
    {
        var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        dbContext.Users.Add(new User
        {
            Email = email,
            DisplayName = "Existing User",
            Country = "NZ",
            PasswordHash = "hashedpassword"
        });
        await dbContext.SaveChangesAsync();
    }

    [Then(@"I receive the error message ""([^""]*)""")]
    public async Task ThenIReceiveTheErrorMessage(string expected)
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.IsSuccessStatusCode.ShouldBeFalse();

        var raw = await
            _fixture.LastResponse.Content.ReadAsStringAsync();
        string? actual;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            actual = doc.RootElement.ValueKind ==
                     JsonValueKind.Object
                ?
                doc.RootElement.GetProperty("message").GetString()
                : doc.RootElement.GetString();
        }
        catch (JsonException)
        {
            actual = raw;
        }
        actual.ShouldBe(expected);
    }
}
