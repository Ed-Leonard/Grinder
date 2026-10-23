using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U6EditProfileSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private UpdateProfileRequest? _request;
    private HttpResponseMessage? _response;
    private string _email = "valid@example.com";
    private string _displayName = "Valid Name";
    private string _country = "NZ";

    public U6EditProfileSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I have entered a valid email, display name, and select a country")]
    public void GivenIHaveEnteredValidProfileDetails()
    {
        _email = "updated@example.com";
        _displayName = "Updated Name";
        _country = "NZ";
    }

    [When(@"I click ""Update""")]
    public async Task WhenIClickUpdate()
    {
        _request = new UpdateProfileRequest(_email, _displayName, _country);
        _response = await _fixture.HttpClient.PutAsJsonAsync("/api/user/profile", _request);
        _fixture.LastResponse = _response;
    }

    [Then(@"I receive a confirmation message")]
    public void ThenIReceiveAConfirmationMessage()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Then(@"my changes are persisted")]
    public async Task ThenMyChangesArePersisted()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId!.Value);
        user.ShouldNotBeNull();
        user.Email.ShouldBe(_email);
        user.DisplayName.ShouldBe(_displayName);
        user.Country.ShouldBe(_country);
    }

    [Given(@"I have entered an malformed email address {string}")]
    public void GivenIHaveEnteredAMalformedEmailAddress(string email)
    {
        _email = email;
    }

    [Then(@"no changes are applied to my profile")]
    public async Task ThenNoChangesAreAppliedToMyProfile()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId!.Value);
        user.ShouldNotBeNull();
        user.Email.ShouldNotBe(_email);
    }

    [Given(@"I enter the email {string} the edit profile form")]
    public void GivenIEnterTheEmailOnTheEditProfileForm(string email)
    {
        _email = email;
    }

    [Given(@"I enter the display name {string} on the edit profile form")]
    public void GivenIEnterTheDisplayNameOnTheEditProfileForm(string name)
    {
        _displayName = name;
    }
}
