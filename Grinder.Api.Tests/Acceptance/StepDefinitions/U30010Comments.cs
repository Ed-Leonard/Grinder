using System.Net;
using System.Net.Http.Json;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Tests.Acceptance.Setup;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using Shouldly;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U30010CommentsSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private TaskItem? _task;
    private HttpResponseMessage? _response;
    private TaskCommentsResponse? _comments;
    private string? _enteredComment;
    private int _postedCommentId;
    private int _parentCommentId;
    private CommentResponse? _replyResponse;

    public U30010CommentsSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given("I am viewing a task with comments enabled")]
    public async Task GivenIAmViewingATaskWithCommentsEnabled()
    {
        _task = new TaskItem
        {
            UserId = _fixture.CurrentUserId!.Value,
            Title = "Task available for comments",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(7).DateTime),
            Visibility = TaskVisibility.Private
        };

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.TaskItems.Add(_task);
        await context.SaveChangesAsync();
    }

    [When("I post the comment {string}")]
    public async Task WhenIPostTheComment(string content)
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _task!.Id,
            Content = content
        });
    }

    [Then("the comment is added to the task with its text, timestamp, and author")]
    public async Task ThenTheCommentIsAddedToTheTaskWithItsTextTimestampAndAuthor()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createdComment = await _response.Content.ReadFromJsonAsync<CommentResponse>();
        createdComment.ShouldNotBeNull();
        createdComment.Content.ShouldBe("Great work! 😀");
        createdComment.TaskId.ShouldBe(_task!.Id);
        createdComment.Author.Id.ShouldBe(_fixture.CurrentUserId!.Value);
        createdComment.Author.DisplayName.ShouldBe("Test User");
        createdComment.CreatedAt.ShouldNotBe(default);
    }

    [Given("I have posted the comment {string}")]
    public async Task GivenIHavePostedTheComment(string content)
    {
        var response = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _task!.Id,
            Content = content
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdComment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        createdComment.ShouldNotBeNull();
        _postedCommentId = createdComment.Id;
    }

    [When("I delete that comment")]
    public async Task WhenIDeleteThatComment()
    {
        _response = await _fixture.HttpClient.DeleteAsync($"api/Comment/{_postedCommentId}");
    }

    [Then("the comment is removed from the task")]
    public async Task ThenTheCommentIsRemovedFromTheTask()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResult = await _response.Content.ReadFromJsonAsync<DeleteCommentResult>();
        deleteResult.ShouldNotBeNull();
        deleteResult.Removed.ShouldBeTrue();

        var getResponse = await _fixture.HttpClient.GetAsync($"api/Comment/task/{_task!.Id}");
        var result = await getResponse.Content.ReadFromJsonAsync<TaskCommentsResponse>();
        result.ShouldNotBeNull();

        result.Comments.ShouldNotContain(c => c.Id == _postedCommentId);
    }

    [Given("the task has comments created at different times")]
    public async Task GivenTheTaskHasCommentsCreatedAtDifferentTimes()
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.Comments.AddRange(
            new Comment
            {
                TaskId = _task!.Id,
                AuthorId = _fixture.CurrentUserId!.Value,
                Content = "Oldest comment",
                CreatedAt = _fixture.AcceptanceTestNow.AddMinutes(-2)
            },
            new Comment
            {
                TaskId = _task.Id,
                AuthorId = _fixture.CurrentUserId.Value,
                Content = "Newest comment",
                CreatedAt = _fixture.AcceptanceTestNow.AddMinutes(-1)
            });
        await context.SaveChangesAsync();
    }

    [When("I view the task comments")]
    public async Task WhenIViewTheTaskComments()
    {
        _response = await _fixture.HttpClient.GetAsync($"api/Comment/task/{_task!.Id}");
        _comments = await _response.Content.ReadFromJsonAsync<TaskCommentsResponse>();
    }

    [Then("the comments are shown in reverse chronological order")]
    public void ThenTheCommentsAreShownInReverseChronologicalOrder()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _comments.ShouldNotBeNull();
        _comments.Comments.Select(comment => comment.Content).ShouldBe(new[]
        {
            "Newest comment",
            "Oldest comment"
        });
    }

    [Given("there is a parent comment on the task")]
    public async Task GivenThereIsAParentCommentOnTheTask()
    {
        var response = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _task!.Id,
            Content = "This is a parent comment"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdComment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        createdComment.ShouldNotBeNull();
        _parentCommentId = createdComment.Id;
    }

    [When("I reply to that comment with {string}")]
    public async Task WhenIReplyToThatCommentWith(string content)
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _task!.Id,
            Content = content,
            ParentCommentId = _parentCommentId
        });
    }

    [Given("I have replied to that comment with {string}")]
    public async Task GivenIHaveRepliedToThatCommentWith(string content)
    {
        var response = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _task!.Id,
            Content = content,
            ParentCommentId = _parentCommentId
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Then("the reply is added to the task and persisted")]
    public async Task ThenTheReplyIsAddedToTheTaskAndPersisted()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.Created);

        _replyResponse = await _response.Content.ReadFromJsonAsync<CommentResponse>();
        _replyResponse.ShouldNotBeNull();
        _replyResponse.Content.ShouldBe("Thanks for the feedback!");
        _replyResponse.TaskId.ShouldBe(_task!.Id);
        _replyResponse.Author.Id.ShouldBe(_fixture.CurrentUserId!.Value);
        _replyResponse.CreatedAt.ShouldNotBe(default);
    }

    [Then("the reply is linked to the parent comment")]
    public void ThenTheReplyIsLinkedToTheParentComment()
    {
        _replyResponse.ShouldNotBeNull();
        _replyResponse.ParentCommentId.ShouldBe(_parentCommentId);
    }

    [When("I fetch the replies for that comment")]
    public async Task WhenIFetchTheRepliesForThatComment()
    {
        _response = await _fixture.HttpClient.GetAsync($"api/Comment/comment/{_parentCommentId}");
    }

    [Then("the reply {string} is returned in the replies list")]
    public async Task ThenTheReplyIsReturnedInTheRepliesList(string expectedContent)
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var paginatedResponse = await _response.Content.ReadFromJsonAsync<PaginatedRepliesResponse>();
        paginatedResponse.ShouldNotBeNull();
        paginatedResponse.Replies.ShouldContain(r => r.Content == expectedContent);
    }

    [Then("the parent comment has a reply count of {int}")]
    public void ThenTheParentCommentHasAReplyCountOf(int expectedCount)
    {
        _comments.ShouldNotBeNull();
        var parentComment = _comments.Comments.FirstOrDefault(c => c.Id == _parentCommentId);
        parentComment.ShouldNotBeNull();
        parentComment.ReplyCount.ShouldBe(expectedCount);
    }

    [Given("I enter a comment longer than 2048 characters long")]
    public void GivenIEnterACommentLongerThan2048CharactersLong()
    {
        _enteredComment = new string('a', 2049);
    }

    [When("I click 'Post'")]
    public async Task WhenIClickPost()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/Comment", new CreateCommentRequest
        {
            TaskId = _task!.Id,
            Content = _enteredComment!
        });
    }

    [Then("I see an error message 'Comment must be 2048 characters or less'")]
    public async Task ThenISeeAnErrorMessage()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await _response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Errors.Values
            .SelectMany(errors => errors)
            .ShouldContain("Comment must be 2048 characters or less");
    }
}