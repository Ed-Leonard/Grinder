using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U3001SubTasksSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private CreateTaskRequest? _request;
    private HttpResponseMessage? _response;
    private TaskResponse? _createdTask;
    private string? _newCategoryName;

    public U3001SubTasksSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I have entered a valid task title, due date, and any other optional fields")]
    public void GivenValidTaskDetails()
    {
        _request = new CreateTaskRequest
        {
            Title = "World Domination Phase 1",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(3)),
            ItemStatus = TaskItemStatus.Todo,
            Visibility = TaskVisibility.Private
        };
    }

    [When(@"I click the ""Create Task"" button")]
    public async Task WhenIClickCreateTask()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/tasks", _request);
        _createdTask = await _response.Content.ReadFromJsonAsync<TaskResponse>();
    }

    [Then(@"the task is created")]
    public void ThenTaskIsCreated()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.Created);
        _createdTask.ShouldNotBeNull();
    }

    [Then(@"is displayed on the dashboard in the tasks")]
    public async Task ThenTaskIsDisplayedOnDashboard()
    {
        var listResponse = await _fixture.HttpClient.GetAsync($"api/tasks/user/{_fixture.CurrentUserId}");
        listResponse.EnsureSuccessStatusCode();
        var tasks = await listResponse.Content.ReadFromJsonAsync<List<TaskResponse>>();
        tasks.ShouldNotBeNull();
        tasks!.ShouldContain(t => t.Id == _createdTask!.Id);
    }

    [Given(@"I am on the create task form")]
    public void GivenIAmOnTheCreateTaskForm()
    {
        _request = new CreateTaskRequest
        {
            Title = "Visibility Test Task",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(3)),
            ItemStatus = TaskItemStatus.Todo
        };
    }

    [When(@"I set the visibility to ""(.*)""")]
    public async Task WhenISetVisibility(string visibility)
    {
        _request!.Visibility = Enum.Parse<TaskVisibility>(visibility);
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/tasks", _request);
        _createdTask = await _response.Content.ReadFromJsonAsync<TaskResponse>();
    }

    [Then(@"the visibility of the task is set accordingly")]
    public void ThenVisibilityIsSetAccordingly()
    {
        _createdTask.ShouldNotBeNull();
        _createdTask!.Visibility.ShouldBe(_request!.Visibility);
    }

    [Given(@"I have not entered a due time")]
    public void GivenIHaveNotEnteredADueTime()
    {
        _request = new CreateTaskRequest
        {
            Title = "No Due Time Task",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(3)),
            DueTime = null,
            ItemStatus = TaskItemStatus.Todo
        };
    }

    [When(@"I create the task")]
    public async Task WhenICreateTheTask()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/tasks", _request);
        _createdTask = await _response.Content.ReadFromJsonAsync<TaskResponse>();
    }

    [Then(@"it shows up as having a due time of 11:59pm on the due date that was set")]
    public void ThenDueTimeDefaultsToEndOfDay()
    {
        _createdTask.ShouldNotBeNull();
        _createdTask!.DueTime.ShouldBe(new TimeOnly(23, 59));
        _createdTask.DueDate.ShouldBe(_request!.DueDate);
    }

    [Given(@"I have entered a start Date")]
    public void GivenIHaveEnteredAStartDate()
    {
        _request = new CreateTaskRequest
        {
            Title = "No Start Time Task",
            StartDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime),
            StartTime = null,
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(3)),
            ItemStatus = TaskItemStatus.Todo
        };
    }

    [Given(@"have not entered a start time")]
    public async Task GivenHaveNotEnteredAStartTime()
    {
        _response = await _fixture.HttpClient.PostAsJsonAsync("api/tasks", _request);
        _createdTask = await _response.Content.ReadFromJsonAsync<TaskResponse>();
    }

    [Then(@"it shows as having a start time of 12:00am on the start date that was set")]
    public void ThenStartTimeDefaultsToMidnight()
    {
        _createdTask.ShouldNotBeNull();
        _createdTask!.StartTime.ShouldBe(new TimeOnly(0, 0));
        _createdTask.StartDate.ShouldBe(_request!.StartDate);
    }

    [Given(@"I am on the Create Task page")]
    public void GivenIAmOnTheCreateTaskPage()
    {
        // No API call required to reach this step
    }

    private TaskResponse? _deepestParent;

    [Given(@"there is a task with 2 or more parents")]
    public async Task GivenThereIsATaskWith2OrMoreParents()
    {
        async Task<TaskResponse> CreateTask(string title, int? parentId)
        {
            var req = new CreateTaskRequest
            {
                Title = title,
                ParentTaskId = parentId,
                DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(3)),
                ItemStatus = TaskItemStatus.Todo
            };
            var res = await _fixture.HttpClient.PostAsJsonAsync("api/tasks", req);
            res.EnsureSuccessStatusCode();
            return (await res.Content.ReadFromJsonAsync<TaskResponse>())!;
        }

        var grandparent = await CreateTask("Grandparent Task", null);
        var parent = await CreateTask("Parent Task", grandparent.Id);
        _deepestParent = await CreateTask("Child Task", parent.Id);
    }

    private List<ValidParentTask>? _validParents;

    [When(@"I attempt to choose a parent task")]
    public async Task WhenIAttemptToChooseAParentTask()
    {
        var response = await _fixture.HttpClient.GetAsync("api/tasks/parents");
        response.EnsureSuccessStatusCode();
        _validParents = await response.Content.ReadFromJsonAsync<List<ValidParentTask>>();
    }

    [Then(@"it does not show up in the drop down as an option for a parent")]
    public void ThenItDoesNotShowUpAsAParentOption()
    {
        _validParents.ShouldNotBeNull();
        _validParents!.ShouldNotContain(p => p.Id == _deepestParent!.Id);
    }

    [Given(@"I am viewing my home page")]
    public void GivenIAmViewingMyHomePage()
    {
    }

    private List<TaskResponse>? _viewedTasks;

    [When(@"I look at my tasks")]
    public async Task WhenILookAtMyTasks()
    {
        var response = await _fixture.HttpClient.GetAsync($"api/tasks/user/{_fixture.CurrentUserId}");
        response.EnsureSuccessStatusCode();
        _viewedTasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>();
    }

    [Then(@"I see all my tasks in chronological order")]
    public void ThenTasksAreInChronologicalOrder()
    {
        _viewedTasks.ShouldNotBeNull();
        var ordered = _viewedTasks!
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.DueTime)
            .ToList();
        _viewedTasks.ShouldBe(ordered);
    }

    [Then(@"any subtasks are clearly nested inside their parent task")]
    public void ThenSubtasksAreNestedInParent()
    {
        _viewedTasks.ShouldNotBeNull();
        foreach (var task in _viewedTasks!.Where(t => t.ParentTaskId != null))
        {
            task.Depth.ShouldBeGreaterThan(0);
        }
    }

    [Given(@"I am creating a task")]
    public void GivenIAmCreatingATask()
    {
        _request = new CreateTaskRequest
        {
            Title = "Task With New Category",
            DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(3)),
            ItemStatus = TaskItemStatus.Todo
        };
    }

    [Given(@"I enter a category that does not match any existing category")]
    public async Task GivenIEnterACategoryThatDoesNotMatchAnExistingCategory()
    {
        _newCategoryName = "Nemesis Planning";

        var existingResponse = await _fixture.HttpClient.GetAsync("api/category");
        existingResponse.EnsureSuccessStatusCode();
        var existing = await existingResponse.Content.ReadFromJsonAsync<List<TaskCategoryResponse>>();
        existing.ShouldNotBeNull();
        existing!.ShouldNotContain(c => c.Name == _newCategoryName);

        var createCategoryResponse = await _fixture.HttpClient.PostAsJsonAsync(
            "api/category", new CreateCategoryRequest(_newCategoryName));
        createCategoryResponse.EnsureSuccessStatusCode();
        var newCategory = await createCategoryResponse.Content.ReadFromJsonAsync<TaskCategoryResponse>();

        _request!.CategoryIds = new List<int> { newCategory!.Id };
    }

    [Then(@"the new category is added to the task")]
    public void ThenTheNewCategoryIsAddedToTheTask()
    {
        _createdTask.ShouldNotBeNull();
        _createdTask!.Categories.ShouldContain(c => c.Name == _newCategoryName);
    }

    [Then(@"I can select this category when creating more tasks")]
    public async Task ThenICanSelectThisCategoryWhenCreatingMoreTasks()
    {
        var response = await _fixture.HttpClient.GetAsync("api/category");
        response.EnsureSuccessStatusCode();
        var categories = await response.Content.ReadFromJsonAsync<List<TaskCategoryResponse>>();
        categories.ShouldNotBeNull();
        categories!.ShouldContain(c => c.Name == _newCategoryName);
    }
}