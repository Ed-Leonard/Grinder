using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U30011ViewClipsSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private TaskItem? _clipTask;
    private User? _otherUser;
    private Reaction? _reaction;
    private ClipPageDto? _clipPage;
    private CommentResponse? _createdComment;
    private ReactionResponse? _reactionResult;

    public U30011ViewClipsSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"another user has posted a public clip")]
    public async Task GivenAnotherUserHasPostedAPublicClip()
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();

        _otherUser = new User
        {
            Email = "other-clipper@test.com",
            DisplayName = "Other Clipper",
            PasswordHash = "unused",
            Country = "NZ",
            IsVerified = true
        };
        context.Users.Add(_otherUser);
        await context.SaveChangesAsync();

        _clipTask = new TaskItem
        {
            UserId = _otherUser.Id,
            Title = "Yoga Routine Practice",
            Description = "Working through my full yoga routine ahead of the recital next week.",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(1).DateTime),
            ItemStatus = TaskItemStatus.Todo,
            Visibility = TaskVisibility.Public,
            IsClip = true,
            ClipCreatedAt = _fixture.AcceptanceTestNow
        };
        context.TaskItems.Add(_clipTask);
        await context.SaveChangesAsync();

        context.TaskAttachments.Add(new TaskAttachment
        {
            TaskItemId = _clipTask.Id,
            UserId = _otherUser.Id,
            StoredFileName = "yoga.mp4",
            ContentType = "video/mp4",
            FileSize = 1024,
            Url = "/uploads/taskAttachments/yoga.mp4",
            CreatedAt = _fixture.AcceptanceTestNow.DateTime
        });
        await context.SaveChangesAsync();
    }

    [When(@"I view the clips page")]
    public async Task WhenIViewTheClipsPage()
    {
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync("api/Clip?page=1&limit=20");
        _fixture.LastResponse.EnsureSuccessStatusCode();
        _clipPage = await _fixture.LastResponse.Content.ReadFromJsonAsync<ClipPageDto>();
    }

    [Then(@"I see that clip in the feed")]
    public void ThenISeeThatClipInTheFeed()
    {
        _clipPage.ShouldNotBeNull();
        _clipPage!.Items.ShouldContain(c => c.Id == _clipTask!.Id);
    }

    [Then(@"it shows the video attachment, the poster's profile info, and the description")]
    public void ThenItShowsTheVideoAttachmentPosterProfileInfoAndDescription()
    {
        var clip = _clipPage!.Items.First(c => c.Id == _clipTask!.Id);
        clip.TaskAttachments.ShouldContain(a => a.ContentType == "video/mp4");
        clip.UserId.ShouldBe(_otherUser!.Id);
        clip.UserDisplayName.ShouldBe(_otherUser.DisplayName);
        clip.Description.ShouldBe(_clipTask!.Description);
    }

    [Given(@"a reaction ""(.*)"" exists")]
    public async Task GivenAReactionExists(string reactionName)
    {
        _reaction = await GetOrCreateReactionAsync(reactionName);
    }

    [Given(@"I have reacted to the clip with ""(.*)""")]
    public async Task GivenIHaveReactedToTheClipWith(string reactionName)
    {
        var response = await _fixture.HttpClient.PostAsync(
            $"api/tasks/{_clipTask!.Id}/reactions/{_reaction!.Id}", null);
        response.EnsureSuccessStatusCode();
    }

    [When(@"I react to the clip with ""(.*)""")]
    [When(@"I react to the clip with ""(.*)"" again")]
    public async Task WhenIReactToTheClipWith(string reactionName)
    {
        _fixture.LastResponse = await _fixture.HttpClient.PostAsync(
            $"api/tasks/{_clipTask!.Id}/reactions/{_reaction!.Id}", null);
        _fixture.LastResponse.EnsureSuccessStatusCode();
        _reactionResult = await _fixture.LastResponse.Content.ReadFromJsonAsync<ReactionResponse>();
    }

    [Then(@"the reaction is added to the clip")]
    public void ThenTheReactionIsAddedToTheClip()
    {
        _reactionResult.ShouldNotBeNull();
        _reactionResult!.ReactedByCurrentUser.ShouldBeTrue();
    }

    [Then(@"the reaction is removed from the clip")]
    public void ThenTheReactionIsRemovedFromTheClip()
    {
        _reactionResult.ShouldNotBeNull();
        _reactionResult!.ReactedByCurrentUser.ShouldBeFalse();
    }

    [Then(@"the reaction count is (\d+)")]
    public void ThenTheReactionCountIs(int expectedCount)
    {
        _reactionResult.ShouldNotBeNull();
        _reactionResult!.Count.ShouldBe(expectedCount);
    }

    [When(@"I post the comment ""(.*)"" on the clip")]
    public async Task WhenIPostTheCommentOnTheClip(string content)
    {
        _fixture.LastResponse = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _clipTask!.Id,
            Content = content
        });
    }

    [Then(@"the comment is added to the clip")]
    public async Task ThenTheCommentIsAddedToTheClip()
    {
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.Created);
        _createdComment = await _fixture.LastResponse.Content.ReadFromJsonAsync<CommentResponse>();
        _createdComment.ShouldNotBeNull();
    }

    [Then(@"I can see it when I view the clip's comments")]
    public async Task ThenICanSeeItWhenIViewTheClipsComments()
    {
        var getResponse = await _fixture.HttpClient.GetAsync($"api/Comment/task/{_clipTask!.Id}");
        getResponse.EnsureSuccessStatusCode();
        var result = await getResponse.Content.ReadFromJsonAsync<TaskCommentsResponse>();
        result.ShouldNotBeNull();
        result.Comments.ShouldContain(c => c.Id == _createdComment!.Id && c.Content == _createdComment!.Content);
    }

    private async Task<Reaction> GetOrCreateReactionAsync(string name)
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var existing = dbContext.Reactions.FirstOrDefault(r => r.Name == name);
        if (existing != null)
        {
            return existing;
        }

        var reaction = new Reaction { Name = name, FileName = $"{name.ToLowerInvariant()}.svg" };
        dbContext.Reactions.Add(reaction);
        await dbContext.SaveChangesAsync();
        return reaction;
    }
}