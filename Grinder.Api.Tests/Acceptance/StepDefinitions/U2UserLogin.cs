using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U2UserLoginSteps
{
    private readonly AcceptanceTestFixture _fixture;

    private LoginRequest? _request;
    private HttpResponseMessage? _response;

    public U2UserLoginSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Given(@"I have registered a new user account")]
    public async Task GivenIHaveRegisteredANewUserAccount()
    {
        await _fixture.RegisterAsTestUser();
    }

    [Given(@"I enter the email and password matching a registered account in the system")]
    public void GivenValidLoginDetails()
    {
        _request = new LoginRequest("test@test.com", "Password123!");
    }

    [Given(@"I enter an email with an invalid format: {string}")]
    public void GivenAInvalidEmail(string email)
    {
        _request = new LoginRequest(email, "Password123!");
    }

    [Given(@"I enter an email that is registered to the system")]
    public void GivenAValidRegisteredEmail()
    {
        _request = new LoginRequest("test@test.com", "");
    }

    [Given(@"a password that does not match this email")]
    public void GivenAnIncorrectPassword()
    {
        _request = _request! with
    {
        Password = "WrongPassword123!"};
    }

    [Given(@"I enter an email that is not registered in the system")]
    public void GivenAEmailNotRegisteredInTheSystem()
    {
        _request = new LoginRequest("no@good.com", "ohno!");
    }

    [When(@"I click the 'Login' button")]
    public async Task WhenIClickTheLoginButton()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("/api/auth/login", _request);
    }

    [Then(@"I am authenticated successfully")]
    public async Task ThenIAmAuthenticatedSuccessfully()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Then(@"I see the an error message saying 'Invalid email address. Email must be in the format 'jane@doe.nz''")]
    public async Task ThenISeeInvalidEmailError()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await _response!.Content.ReadAsStringAsync();
        body.ShouldContain("Email must be in the format");
    }

    [Then(@"I see an error message 'Invalid email or password'")]
    public async Task ThenISeeInvalidLoginError()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var body = await _response!.Content.ReadAsStringAsync();
        body.ShouldContain("Invalid email or password");
    }
}
