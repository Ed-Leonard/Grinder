using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U1RegisterSteps
{
    private readonly AcceptanceTestFixture _fixture;

    private RegisterRequest? _request;
    private HttpResponseMessage? _response;

    public U1RegisterSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }


    [Given(@"I enter a valid email, display name, an acceptable password, a repeated password matching the other password, and select a country from a list of options")]
    public void GivenValidRegistrationDetails()
    {
        _request = new RegisterRequest("test@email.com", "Test User", "Password123!", "NZ");
    }

    [When(@"I click ""Register""")]
    public async Task WhenIClickRegister()
    {

        _response = await _fixture.HttpClient.PostAsJsonAsync("/api/auth/register", _request);
        _fixture.LastResponse = _response;
    }

    [Then(@"I see a confirmation message")]
    public void ThenConfirmationMessage()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Then(@"my login data is securely persisted")]
    public async Task ThenDataPersisted()
    {
        var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == _request.Email);
        user.ShouldNotBeNull();
        user.DisplayName.ShouldBe(_request?.DisplayName);
        user.Country.ShouldBe(_request?.Country);
    }

    [Given(@"I enter the email {string} on the register form")]
    public void GivenEmail(string email)
    {
        _request = new RegisterRequest(email, "Test User", "Password123!", "NZ");
    }

    [Then(@"I receive an error message")]
    public async Task ThenInvalidEmailError()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Given(@"I enter the password {string} on the register form")]
    public void GivenPassword(string password)
    {
        _request = new RegisterRequest("email@test.com", "Test User", password, "NZ");
    }

    [Given(@"I enter the display name {string} on the register form")]
    public void GivenDisplayName(string displayName)
    {
        _request = new RegisterRequest("email@test.com", displayName, "Password123", "NZ");
    }
}
