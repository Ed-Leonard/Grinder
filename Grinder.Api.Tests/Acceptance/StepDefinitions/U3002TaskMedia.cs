using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Tests.Acceptance.Setup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Shouldly;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U3002AttachmentSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private TaskItem? _task;
    private HttpResponseMessage? _response;
    private TaskAttachment? _uploadedAttachment;
    private TaskResponse? _createdTask;
    private readonly List<int> _pendingAttachmentIds = new();
    private int? _stagedRemovalId;

    private (string FileName, string ContentType, byte[] Content)? _selectedFile;

    public U3002AttachmentSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<HttpResponseMessage> UploadFileAsync(string fileName, string contentType, byte[] content)
    {
        StampSignature(content, fileName);
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return await _fixture.HttpClient.PostAsync("api/AttachmentUploads", form);
    }

    private static byte[] MakeBytes(long size) => new byte[size];

    // Stamps real magic-number bytes into the buffer (when the extension has one) so
    // AttachmentValidator's byte-signature check has genuine content to match against.
    // No-op for extensions with no rule (e.g. .avi, .bmp) since those are rejected on
    // extension/content-type lookup before the byte check ever runs.
    private static void StampSignature(byte[] content, string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        byte[]? signature = extension switch
        {
            ".png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            ".jpg" or ".jpeg" => [0xFF, 0xD8, 0xFF],
            ".gif" => "GIF89a"u8.ToArray(),
            ".webm" => [0x1A, 0x45, 0xDF, 0xA3],
            _ => null
        };

        if (signature != null && content.Length >= signature.Length)
        {
            signature.CopyTo(content, 0);
        }

        if (extension is ".mp4" or ".mov" && content.Length >= 8)
        {
            "ftyp"u8.ToArray().CopyTo(content, 4);
        }
    }


    [Given("I have chosen a video")]
    public async Task GivenIHaveChosenAVideo()
    {
        _response = await UploadFileAsync("adventure.mp4", "video/mp4", MakeBytes(1 * 1024 * 1024));
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);

        _uploadedAttachment = await _response.Content.ReadFromJsonAsync<TaskAttachment>();
        _uploadedAttachment.ShouldNotBeNull();
        _pendingAttachmentIds.Add(_uploadedAttachment.Id);
    }

    [Given("I am on the confirm attachments modal with a preview of the video")]
    public void GivenIAmOnTheConfirmAttachmentsModalWithAPreviewOfTheVideo()
    {
    }

    [When("I click the 'Upload' button")]
    public async Task WhenIClickTheUploadButton()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/Tasks", new CreateTaskRequest
        {
            Title = "Adventure task",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(1).DateTime),
            ItemStatus = TaskItemStatus.Todo,
            Visibility = TaskVisibility.Private,
            AttachmentIds = new List<int>(_pendingAttachmentIds)
        });

        if (_response.StatusCode == HttpStatusCode.Created)
        {
            _createdTask = await _response.Content.ReadFromJsonAsync<TaskResponse>();
        }
    }

    [Then("my video is uploaded to my task")]
    public void ThenMyVideoIsUploadedToMyTask()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.Created);
        _createdTask.ShouldNotBeNull();
        _createdTask.TaskAttachments.ShouldContain(a => a.Id == _uploadedAttachment!.Id);
    }


    [Given("I choose a video that is more than 50MB in size")]
    public void GivenIChooseAVideoThatIsMoreThan50MbInSize()
    {
        _selectedFile = ("too-big.mp4", "video/mp4", MakeBytes(51 * 1024 * 1024));
    }

    [When("I confirm and click 'Upload'")]
    public async Task WhenIConfirmAndClickUpload()
    {
        _selectedFile.ShouldNotBeNull();
        _response = await UploadFileAsync(_selectedFile.Value.FileName, _selectedFile.Value.ContentType, _selectedFile.Value.Content);
    }

    [Then("I see an error message saying 'Video is too large, please choose a smaller file'")]
    public async Task ThenISeeAnErrorMessageSayingVideoIsTooLarge()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await _response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body.ShouldNotBeNull();
        body["code"].ShouldBe("VideoTooLarge");
    }

    [Then("my video is not uploaded")]
    public void ThenMyVideoIsNotUploaded()
    {
        _response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }


    [Given("the combined total of existing attachments and attachments I am attempting to upload is greater than 5")]
    public async Task GivenTheCombinedTotalOfAttachmentsIsGreaterThan5()
    {
        _task = new TaskItem
        {
            UserId = _fixture.CurrentUserId!.Value,
            Title = "Task with many attachments",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(7).DateTime),
            Visibility = TaskVisibility.Private
        };

        await using (var context = await _fixture.DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.Add(_task);
            await context.SaveChangesAsync();

            for (int i = 0; i < 5; i++)
            {
                context.TaskAttachments.Add(new TaskAttachment
                {
                    TaskItemId = _task.Id,
                    UserId = _fixture.CurrentUserId.Value,
                    StoredFileName = $"existing-{i}.png",
                    ContentType = "image/png",
                    FileSize = 1024,
                    Url = $"/uploads/taskAttachments/existing-{i}.png",
                    CreatedAt = _fixture.AcceptanceTestNow.DateTime
                });
            }
            await context.SaveChangesAsync();
        }

        var uploadResponse = await UploadFileAsync("extra.png", "image/png", MakeBytes(1024));
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<TaskAttachment>();
        _pendingAttachmentIds.Add(attachment!.Id);
    }

    [When("I click 'Upload'")]
    [Scope(Feature = "U3002 - As Indiana, I want to upload images and videos of my adventures to tasks so that I can share them with others and refer to them later for further research.")]
    public async Task WhenIClickUpload()
    {
        _response = await _fixture.HttpClient.PatchAsJsonAsync($"api/Tasks/{_task!.Id}", new PatchTaskRequest
        {
            AttachmentIds = new List<int>(_pendingAttachmentIds)
        });
    }

    [Then("I see an error message saying 'Maximum of 5 attachments allowed'")]
    public async Task ThenISeeAnErrorMessageSayingMaximumOf5AttachmentsAllowed()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await _response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();

        problem.Errors.Values
            .SelectMany(errors => errors)
            .ShouldContain(error => error.Contains("Maximum of 5 attachments allowed"));
    }


    [Given("I am on the edit task page")]
    public async Task GivenIAmOnTheEditTaskPage()
    {
        _task = new TaskItem
        {
            UserId = _fixture.CurrentUserId!.Value,
            Title = "Task with an attachment",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(7).DateTime),
            Visibility = TaskVisibility.Private
        };

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.TaskItems.Add(_task);
        await context.SaveChangesAsync();

        var uploadResponse = await UploadFileAsync("removable.png", "image/png", MakeBytes(1024));
        _uploadedAttachment = await uploadResponse.Content.ReadFromJsonAsync<TaskAttachment>();

        await _fixture.HttpClient.PatchAsJsonAsync($"api/Tasks/{_task.Id}", new PatchTaskRequest
        {
            AttachmentIds = new List<int> { _uploadedAttachment!.Id }
        });
    }

    [When("I click the x icon on an attachment")]
    public void WhenIClickTheXIconOnAnAttachment()
    {
        _stagedRemovalId = _uploadedAttachment!.Id;
    }

    [When("I click 'Update Task'")]
    public async Task WhenIClickUpdateTask()
    {
        _stagedRemovalId.ShouldNotBeNull();
        _response = await _fixture.HttpClient.DeleteAsync($"api/AttachmentUploads/{_stagedRemovalId}");
        await _fixture.HttpClient.PatchAsJsonAsync($"api/Tasks/{_task!.Id}", new PatchTaskRequest
        {
            AttachmentIds = new List<int>()
        });
    }


    [Then("the attachment is removed from the task")]
    public void ThenTheAttachmentIsRemovedFromTheTask()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Then(@"the change is persisted\.?")]
    public async Task ThenTheChangeIsPersisted()
    {
        var getResponse = await _fixture.HttpClient.GetAsync($"api/Tasks/{_task!.Id}");
        var task = await getResponse.Content.ReadFromJsonAsync<TaskResponse>();
        task!.TaskAttachments.ShouldNotContain(a => a.Id == _uploadedAttachment!.Id);
    }


    [Given("I have uploaded a single video to a task")]
    public async Task GivenIHaveUploadedASingleVideoToATask()
    {
        var uploadResponse = await UploadFileAsync("clip.mp4", "video/mp4", MakeBytes(1 * 1024 * 1024));
        var attachment = await uploadResponse.Content.ReadFromJsonAsync<TaskAttachment>();
        _pendingAttachmentIds.Add(attachment!.Id);
    }

    [When("I create the task")]
    [Scope(Feature = "U3002 - As Indiana, I want to upload images and videos of my adventures to tasks so that I can share them with others and refer to them later for further research.")]
    public async Task WhenICreateTheTask()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/Tasks", new CreateTaskRequest
        {
            Title = "Clip task",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(1).DateTime),
            ItemStatus = TaskItemStatus.Todo,
            Visibility = TaskVisibility.Private,
            AttachmentIds = new List<int>(_pendingAttachmentIds)
        });

        if (_response.StatusCode == HttpStatusCode.Created)
        {
            _createdTask = await _response.Content.ReadFromJsonAsync<TaskResponse>();
        }
    }

    [Then("it is automatically classified as a clip")]
    public async Task ThenItIsAutomaticallyClassifiedAsAClip()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.Created);
        _createdTask.ShouldNotBeNull();

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        var task = await context.TaskItems.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _createdTask.Id);

        task.ShouldNotBeNull();
        task.IsClip.ShouldBeTrue();
    }


    [Given("I have selected a video file that is not of type mp4, mov, or webm")]
    public void GivenIHaveSelectedAnUnsupportedVideoFile()
    {
        _selectedFile = ("clip.avi", "video/x-msvideo", MakeBytes(1024));
    }

    [Given("I have selected an image that is not of type png, jpg, svg, or gif")]
    public void GivenIHaveSelectedAnUnsupportedImageFile()
    {
        _selectedFile = ("photo.bmp", "image/bmp", MakeBytes(1024));
    }

    [When("I confirm the selection")]
    public async Task WhenIConfirmTheSelection()
    {
        _selectedFile.ShouldNotBeNull();
        _response = await UploadFileAsync(_selectedFile.Value.FileName, _selectedFile.Value.ContentType, _selectedFile.Value.Content);
    }

    [Then("I see the error message \"The video must be of type mp4, mov, or webm.\"")]
    public async Task ThenISeeTheUnsupportedVideoErrorMessage()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await _response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["code"].ShouldBe("UnsupportedVideoType");
    }

    [Then("I see the error message \"The image must be of type png, jpg, svg, or gif\"")]
    public async Task ThenISeeTheUnsupportedImageErrorMessage()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await _response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        body!["code"].ShouldBe("UnsupportedImageType");
    }
}