using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U9ChangePasswordSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private HttpResponseMessage? _response;
    private string? _originalPasswordHash;
    private string _oldPassword = "Password123!";
    private string _newPassword = "NewValid@Password1";
    private string _confirmPassword = "NewValid@Password1";
    private string? _verificationCode;

    public U9ChangePasswordSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I click 'Update password'")]
    public async Task GivenIClickUpdatePassword()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/verificationcode/generate", new
        {
            Email = "test@test.com",
            Type = CodeType.PasswordUpdate
        });
    }

    [Given(@"I have received the change code by email")]
    public async Task GivenIHaveReceivedTheChangeCodeByEmail()
    {
        await _fixture.HttpClient.PostAsJsonAsync("api/verificationcode/generate", new
        {
            Email = "test@test.com",
            Type = CodeType.PasswordUpdate
        });
        _verificationCode = GetCurrentCodeFromEmail();
    }

    [Given(@"I enter my old password that matches the password on file")]
    public async Task GivenIEnterMyOldPasswordThatMatches()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId);
        user.ShouldNotBeNull();
        _originalPasswordHash = user!.PasswordHash;
        _oldPassword = "Password123!";
    }

    [Given(@"I enter twice the same acceptable password")]
    public void GivenIEnterTwiceTheSameAcceptablePassword()
    {
        _newPassword = "NewValid@Password1";
        _confirmPassword = "NewValid@Password1";
    }

    [Given(@"I enter an old password that does not match the password on file")]
    public void GivenIEnterAnOldPasswordThatDoesNotMatch()
    {
        _oldPassword = "WrongPassword123!";
        _newPassword = "NewValid@Password1";
        _confirmPassword = "NewValid@Password1";
    }

    [Given(@"I enter two new passwords that do not match")]
    public void GivenIEnterTwoNewPasswordsThatDoNotMatch()
    {
        _oldPassword = "Password123!";
        _newPassword = "NewValid@Password1";
        _confirmPassword = "DifferentPassword2!";
    }

    [Given(@"I enter a new password that does not contain at least 8 characters")]
    public void GivenIEnterAWeakPassword()
    {
        _oldPassword = "Password123!";
        _newPassword = "weak";
        _confirmPassword = "weak";
    }

    [Given(@"contains at least one of each uppercase, lowercase, numbers and special characters")]
    public void GivenPasswordMissingComplexity()
    {
        // Combined with above Given, password already set to "weak"
    }

    [Given(@"I click 'Cancel'")]
    public async Task GivenIClickCancel()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId);
        user.ShouldNotBeNull();
        _originalPasswordHash = user!.PasswordHash;
    }

    [When(@"I enter a valid code corresponding to my change-password request")]
    public async Task WhenIEnterAValidCode()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/verificationcode/verify", new
        {
            Email = "test@test.com",
            Code = _verificationCode,
            Type = CodeType.PasswordUpdate
        });
    }

    [When(@"I click 'Update Password'")]
    public async Task WhenIClickUpdatePassword()
    {
        await _fixture.HttpClient.PostAsJsonAsync("api/verificationcode/generate", new
        {
            Email = "test@test.com",
            Type = CodeType.PasswordUpdate
        });

        await Task.Delay(100);
        _verificationCode = GetCurrentCodeFromEmail();

        var verifyResponse = await _fixture.HttpClient.PostAsJsonAsync("api/verificationcode/verify", new
        {
            Email = "test@test.com",
            Code = _verificationCode,
            Type = CodeType.PasswordUpdate
        });
        
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        _response = await _fixture.HttpClient.PutAsJsonAsync("api/user/update-password", new UpdatePasswordRequest(
            _newPassword,     
            _oldPassword,      
            _confirmPassword   
        ));

    }

    [Then(@"an email is sent to me with a unique code to update my password")]
    public void ThenAnEmailIsSentWithUniqueCode()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.OK);
        _fixture.EmailService.SentEmails.ShouldNotBeEmpty();
    }

    [Then(@"the code is validated")]
    public void ThenIAmPresentedWithChangePasswordForm()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Then(@"the password has been updated successfully")]
    public async Task ThenThePasswordHasBeenUpdatedSuccessfully()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId);
        user.ShouldNotBeNull();
        user!.PasswordHash.ShouldNotBe(_originalPasswordHash);
    }

    [Then(@"an error message tells me ""(.*)""")]
    public async Task ThenAnErrorMessageTellsMe(string message)
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        var body = await _response!.Content.ReadAsStringAsync();
        body.ShouldContain(message, Case.Insensitive);
    }

    [Then(@"no changes are applied to my password")]
    public async Task ThenNoChangesAreAppliedToMyPassword()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId);
        user.ShouldNotBeNull();
        user!.PasswordHash.ShouldBe(_originalPasswordHash);
    }

    private string GetCurrentCodeFromEmail()
    {
        var email = _fixture.EmailService.SentEmails.LastOrDefault();
        email.ShouldNotBe(default);
        
        var line = email.Body
            .Split('\n')
            .FirstOrDefault(l => l.Contains("code is:"));
        
        line.ShouldNotBeNullOrEmpty();
        return line!.Split("code is:")[1].Trim();
    }

}
