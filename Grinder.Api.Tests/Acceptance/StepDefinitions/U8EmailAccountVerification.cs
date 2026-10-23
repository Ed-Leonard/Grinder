using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;   
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U8EmailAccountVerificationSteps
{
    private readonly AcceptanceTestFixture _fixture;
    
    private string? _currentEmail;
    private string? _currentPassword;
    private string? _lastSeededCode;

    public U8EmailAccountVerificationSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Given(@"I have registered a new account with email ""([^""]*)""")]
    public async Task GivenIHaveRegisteredWithEmail(string 
        email)
    {
        _currentEmail = email;
        _currentPassword = "Password123!";
        var req = new RegisterRequest(email, "Test User",
            _currentPassword, "NZ");
        var resp = await
            _fixture.HttpClient.PostAsJsonAsync("/api/auth/register", req);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
    
    [Given(@"I have registered a new account with email ""([^""]*)"" and password ""([^""]*)""")]
    public async Task 
        GivenIHaveRegisteredWithEmailAndPassword(string email, string 
            password) 
    {
        _currentEmail = email;
        _currentPassword = password;
        var req = new RegisterRequest(email, "Test User",
            password, "NZ");
        var resp = await
            _fixture.HttpClient.PostAsJsonAsync("/api/auth/register", req);
        resp.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Given(@"a one-time code ""([^""]*)"" has been issued for ""([^""]*)""")]
    public async Task GivenCodeHasBeenIssued(string code, string email)
    {
        await using var context = await
            _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await
            context.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == email);

        var existing = context.VerificationCodes.Where(vc => vc.UserId == user.Id
                                                             && vc.Type == CodeType.EmailVerification);
        context.VerificationCodes.RemoveRange(existing);

        context.VerificationCodes.Add(new VerificationCode
        {
            Code = code,
            Type = CodeType.EmailVerification,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            UserId = user.Id
        });

        await context.SaveChangesAsync();
        _lastSeededCode = code;
    }
    
    [Given(@"5 minutes have elapsed since the code was issued")]
    public async Task GivenFiveMinutesElapsed()
    {
        await using var ctx = await
            _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await
            ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Email ==
                                                           _currentEmail);
        var code = await ctx.VerificationCodes.FirstAsync(vc =>
            vc.UserId == user.Id && vc.Type ==
            CodeType.EmailVerification);  
        code.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await ctx.SaveChangesAsync();
    }
    
    
    [Given(@"I have not yet confirmed my email")]
    public async Task GivenNotYetConfirmed()
    {
        await using var ctx = await
            _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await
            ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Email ==
                                                           _currentEmail);
        user.IsVerified.ShouldBeFalse();
    }
    
    [When(@"I request a new one-time code for ""([^""]*)""")]
    public async Task WhenIRequestNewCode(string email)
    {
        var req = new
            GenerateVerificationCodeRequest(CodeType.EmailVerification,
                email);   
        _fixture.LastResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/VerificationCode/generate", req);
    }
    
    [When(@"I submit the code ""([^""]*)"" for ""([^""]*)""")]
    public async Task WhenISubmitCode(string code, string 
        email)
    {
        var req = new VerifyCodeRequest(email, code,
            CodeType.EmailVerification);
        _fixture.LastResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", req);
    }

    [When(@"I attempt to log in as ""([^""]*)"" with password ""([^""]*)""")]
    public async Task WhenIAttemptToLogIn(string email, string 
        password)
    {
        var req = new LoginRequest(email, password);
        _fixture.LastResponse = await
            _fixture.HttpClient.PostAsJsonAsync("/api/auth/login", req);
    }
    
    [Then(@"a one-time verification code is stored for my account")]
    public async Task ThenCodeIsStoredForMyAccount()
    {
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.Created);
        await using var ctx = await
            _fixture.DbContextFactory.CreateDbContextAsync();
        var anyCode = await ctx.VerificationCodes.AnyAsync(vc
            => vc.Type == CodeType.EmailVerification);
        anyCode.ShouldBeTrue();
    }
    
    [Then(@"a different one-time code is stored for ""([^""]*)""")]
    public async Task ThenDifferentCodeIsStored(string email)
    {
  
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var ctx = await
            _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await
            ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Email ==
                                                           email);   
        var codes = await ctx.VerificationCodes.Where(vc =>
            vc.UserId == user.Id && vc.Type ==
            CodeType.EmailVerification).ToListAsync();
        codes.Count.ShouldBe(1);
        codes.Single().Code.ShouldNotBe(_lastSeededCode);
    }
    
    [Then(@"submitting the code ""([^""]*)"" for ""([^""]*)"" is rejected with ""([^""]*)""")]
    public async Task ThenSubmittingCodeIsRejected(string code,
        string email, string message)
    {
        var req = new VerifyCodeRequest(email, code,
            CodeType.EmailVerification);
        var resp = await _fixture.HttpClient.PostAsJsonAsync("/api/VerificationCode/verify", req);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await
            resp.Content.ReadFromJsonAsync<JsonElement>();
  
        body.GetProperty("message").GetString().ShouldBe(message);
    }
    
    [Then(@"my account is marked as verified")]
    public async Task ThenAccountIsVerified()
    {

        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var ctx = await
            _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await
            ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Email ==
                                                           _currentEmail);
        user.IsVerified.ShouldBeTrue();
    }
    
    [Then(@"I can log in successfully")]
    public async Task ThenICanLogInSuccessfully()
    {
        var req = new LoginRequest(_currentEmail!,
            _currentPassword!);
        var resp = await
            _fixture.HttpClient.PostAsJsonAsync("/api/auth/login", req);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
