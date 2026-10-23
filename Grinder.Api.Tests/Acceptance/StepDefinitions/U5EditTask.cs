using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U5EditTaskSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private PatchTaskRequest? _request;
    private HttpResponseMessage? _response;
    private TaskItem? _taskItem;

    public U5EditTaskSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I have a task item")]
    public async Task GivenIHaveATaskItem()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        _taskItem = new TaskItem
        {
            Title = "Initial Title",
            UserId = _fixture.CurrentUserId ?? 0
        };
        dbContext.TaskItems.Add(_taskItem);
        await dbContext.SaveChangesAsync();
    }

    [Given(@"I enter a valid title, an optional description, an optional due date, an optional due time, and a status from the options 'Todo', 'In Progress', and 'Done'")]
    public void GivenIEnterAValidTitleAnOptionalDescriptionAnOptionalDueDateAnOptionalDueTimeAndAStatusFromTheOptions()
    {
        _request = new PatchTaskRequest
        {
            Title = "New title",
            Description = "This is a test task.",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5)).ToString("yyyy-MM-dd"),
            DueTime = TimeOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddHours(5)).ToString("HH:mm"),
            ItemStatus = (TaskItemStatus?)1
        };
    }

    [Given(@"I leave the description empty")]
    public void GivenILeaveTheDescriptionEmpty()
    {
        _request = new PatchTaskRequest
        {
            Title = "My Task",
            Description = "",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5)).ToString("yyyy-MM-dd"),
            DueTime = TimeOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddHours(5)).ToString("HH:mm"),
            ItemStatus = 0
        };
    }

    [Given(@"I enter a title that is not between 3 and 128 characters")]
    public void GivenIEnterATitleThatIsNotBetween3And128Characters()
    {
        _request = new PatchTaskRequest
        {
            Title = "a",
            Description = "",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5)).ToString("yyyy-MM-dd"),
            DueTime = TimeOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddHours(5)).ToString("HH:mm"),
            ItemStatus = 0
        };
    }

    [Given(@"I enter a description longer than 2048 characters")]
    public void GivenIEnterADescriptionLongerThan2048Characters()
    {
        _request = new PatchTaskRequest
        {
            Title = "Valid Title",
            Description = new string('a', 2049),
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5)).ToString("yyyy-MM-dd"),
            DueTime = TimeOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddHours(5)).ToString("HH:mm"),
            ItemStatus = 0
        };
    }

    [Given(@"I enter a due date in the past")]
    public void GivenIEnterADueDateInThePast()
    {
        _request = new PatchTaskRequest
        {
            Title = "Valid Title",
            Description = new string('a', 2049),
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(-1)).ToString("yyyy-MM-dd"),
            DueTime = TimeOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddHours(5)).ToString("HH:mm"),
            ItemStatus = 0
        };
    }

    [Given(@"I click the cancel button")]
    public async Task WhenIClickTheCancelButton()
    {
        _response = null;
    }

    [When(@"I click 'Update'")]
    public async Task WhenIClickUpdate()
    {
        _response = await _fixture.HttpClient.PatchAsJsonAsync($"api/tasks/{_taskItem!.Id}", _request);
    }

    [Then(@"the task is updated")]
    public async Task ThenTheTaskIsUpdated()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var task = await dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == _taskItem!.Id);
        task.ShouldNotBeNull();
        task.Title.ShouldBe(_request?.Title);
        task.Description.ShouldBe(_request?.Description);
        task.DueDate!.ToString("yyyy-MM-dd").ShouldBe(_request?.DueDate);
        task.DueTime?.ToString("HH:mm").ShouldBe(_request?.DueTime);
        task.ItemStatus.ShouldBeEquivalentTo(_request?.ItemStatus);
    }

    [Then(@"the Task description is set to empty")]
    public async Task ThenTheTaskDisplaysATextSayingNoDescription()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var task = await dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == _taskItem!.Id);
        task.ShouldNotBeNull();
        task.Title.ShouldBe(_request?.Title);
        task.Description.ShouldBe("");
    }

    [Then(@"I see an error message 'Title is required and must be between 3 and 128 characters long'")]
    public async Task ThenISeeAnErrorMessageTitleIsRequiredAndMustBeBetween3And128CharactersLong()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await _response!.Content.ReadAsStringAsync();
        body.ShouldNotBeNullOrEmpty();
        body.ShouldContain("Title is required", Case.Insensitive);
    }

    [Then(@"I see an error message 'Description must be 2048 characters or less'")]
    public async Task ThenISeeAnErrorMessageDescriptionMustBe2048CharactersOrLess()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await _response!.Content.ReadAsStringAsync();
        body.ShouldContain("Description must be 2048 characters or less");
    }

    [Then(@"I see an error message 'Invalid due date, date must be formatted dd/MM/yyyy'")]
    public async Task ThenISeeAnErrorMessageInvalidDueDateDateMustBeFormattedDdMMYyyy()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var body = await _response!.Content.ReadAsStringAsync();
        body.ShouldContain("date must be formatted");
    }

    [Then(@"I see an error message 'Invalid due date, date must be in the future'")]
    public async Task ThenISeeAnErrorMessageInvalidDueDateDateMustBeInTheFuture()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Then(@"no changes are saved")]
    public async Task ThenNoChangesAreSaved()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var task = await dbContext.TaskItems.FirstOrDefaultAsync(t => t.Id == _taskItem!.Id);

        task.ShouldNotBeNull();
        task.Title.ShouldBe(_taskItem!.Title);
        task.Description.ShouldBe(_taskItem!.Description);
    }
}
