using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U7UserProfilePictureSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private MultipartFormDataContent? _content;
    private HttpResponseMessage? _response;

    public U7UserProfilePictureSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I have selected a valid image of type ""(.*)"" with extension ""(.*)""")]
    public void GivenIHaveSelectedAValidImage(string contentType, string extension)
    {
        BuildFileContent(SignatureBytesFor(extension), contentType, $"avatar{extension}");
    }

    [Given(@"I have selected an image with content type ""(.*)"" and extension ""(.*)""")]
    public void GivenIHaveSelectedAnInvalidImage(string contentType, string extension)
    {
        // Rejected on extension/content-type lookup before any byte-signature check runs,
        // so the content here doesn't need to resemble a real file.
        BuildFileContent(new byte[] { 1, 2, 3, 4 }, contentType, $"avatar{extension}");
    }

    [Given(@"I have selected a valid image that is larger than 5MB")]
    public void GivenIHaveSelectedAValidImageThatIsLargerThan5MB()
    {
        var bytes = new byte[5 * 1024 * 1024 + 1];
        SignatureBytesFor(".png").CopyTo(bytes, 0);
        BuildFileContent(bytes, "image/png", "avatar.png");
    }

    // Builds real magic-number bytes for the given extension so AttachmentValidator's
    // byte-signature check has genuine content to match against.
    private static byte[] SignatureBytesFor(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0),
            ".jpg" or ".jpeg" => Pad([0xFF, 0xD8, 0xFF], 0),
            ".gif" => Pad("GIF89a"u8.ToArray(), 0),
            ".svg" => "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray(),
            _ => [1, 2, 3, 4]
        };
    }

    private static byte[] Pad(byte[] signature, int offset)
    {
        byte[] buffer = new byte[16];
        signature.CopyTo(buffer, offset);
        return buffer;
    }

    [When(@"I submit the profile picture")]
    public async Task WhenISubmitTheProfilePicture()
    {
        _response = await _fixture.HttpClient.PutAsync("api/User/profile/upload", _content);
        _fixture.LastResponse = _response;
    }

    [Then(@"my profile picture is persisted")]
    public async Task ThenMyProfilePictureIsPersisted()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == _fixture.CurrentUserId);
        user.ShouldNotBeNull();
        user.ProfilePictureUrl.ShouldNotBeNullOrEmpty();
    }

    [Then(@"I receive an error message ""(.*)""")]
    public async Task ThenIReceiveAnErrorMessage(string expected)
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await _response.Content.ReadAsStringAsync();
        body.ShouldContain(expected);
    }

    private void BuildFileContent(byte[] bytes, string contentType, string fileName)
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        _content = new MultipartFormDataContent
        {
            { fileContent, "file", fileName }
        };
    }
}
