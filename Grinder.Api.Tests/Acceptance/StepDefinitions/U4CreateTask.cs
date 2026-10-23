using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U4CreateTaskSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private CreateTaskRequest? _request;
    private HttpResponseMessage? _response;

    public U4CreateTaskSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I have entered valid task details for all fields")]
    public void GivenIHaveEnteredValidTaskDetailsForAllFields()
    {
        _request = new CreateTaskRequest
        {
            Title = "Test Task",
            Description = "This is a test task.",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5)),
            DueTime = TimeOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddHours(5)),
            ItemStatus = 0
        };
    }

    [Given(@"I enter no description")]
    public void GivenIEnterNoDescription()
    {
        _request?.Description = "";
    }

    [Given(@"I enter a title that is ""(.*)""")]
    public void GivenIEnterATitleThatIs(string reason)
    {
        _request!.Title = reason == "less than 3 characters" ? "hi" : new string('a', 129);
    }

    [Given(@"I enter a description that is too long")]
    public void GivenIEnterADescriptionThatIsTooLong()
    {
        _request!.Description = new string('a', 2049);
    }

    [Given(@"I enter a due date that is in the past")]
    public void GivenIEnterADueDateThatIsInThePast()
    {
        _request?.DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(-6));
    }

    [When(@"I click ""Create Task""")]
    public async Task WhenIClickCreateTask()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/tasks", _request);
        _fixture.LastResponse = _response;
    }

    [When(@"I click on a task")]
    public async Task WhenIClickOnATask()
    {
        var createdResponse = await _fixture.HttpClient.PostAsJsonAsync("api/tasks/", _request);
        createdResponse.EnsureSuccessStatusCode();

        var created = await createdResponse.Content.ReadFromJsonAsync<TaskResponse>();
        _fixture.currentTaskId = created!.Id;

        _response = await _fixture.HttpClient.GetAsync("api/tasks/" + _fixture.currentTaskId);
    }

    [Then(@"the task is persisted")]
    public async Task ThenTheTaskIsPersisted()
    {
        var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var task = await dbContext.TaskItems.FirstOrDefaultAsync(t => t.Title == _request!.Title);
        task.ShouldNotBeNull();
        task.Title.ShouldBe(_request!.Title);
        task.Description.ShouldBe(_request!.Description);
        task.DueDate.ShouldBe(_request!.DueDate);
        task.DueTime.ShouldBe(_request!.DueTime);
        task.ItemStatus.ShouldBeEquivalentTo(_request!.ItemStatus);
        _fixture.currentTaskId = task.Id;
    }

    [Then(@"I can see its title, full description, due date, due time, status, and creation timestamp")]
    public async Task ThenICanSeeItsTitleFullDescriptionDueDateDueTimeStatusAndCreationTimestamp()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var task = await _response.Content.ReadFromJsonAsync<TaskResponse>();
        task.ShouldNotBeNull();
        task.Title.ShouldBe(_request?.Title);
        task.Description.ShouldBe(_request?.Description);
        task.DueDate.ShouldBe(_request!.DueDate);
        task.DueTime.ShouldBe(_request!.DueTime as TimeOnly?);
        task.ItemStatus.ShouldBeEquivalentTo(_request?.ItemStatus);
        task.CreatedAt.ShouldNotBe(default(DateTimeOffset));
    }
}
