using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using Grinder.Api.Models.Entities;
using System.Net.Http.Json;
using Shouldly;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U3009PublicTasksSteps
{
    private readonly AcceptanceTestFixture _fixture;

    private TaskItem? _currentTask;
    private Reaction? _selectedReaction;
    private ReactionResponse? _toggleResult;

    public U3009PublicTasksSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given(@"I have a task")]
    public async Task GivenIHaveATaskItem()
    {
        var userId = await GetTestUserIdAsync();

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var task = new TaskItem
        {
            Title = "Test Task",
            Description = "A task for acceptance testing",
            ItemStatus = TaskItemStatus.Todo,
            Visibility = TaskVisibility.Public,
            CreatedAt = DateTimeOffset.UtcNow,
            UserId = userId
        };
        dbContext.TaskItems.Add(task);
        await dbContext.SaveChangesAsync();

        _currentTask = task;
    }

    [Given(@"I am viewing a task")]
    public void GivenIAmViewingATask()
    {
        _currentTask.ShouldNotBeNull("Expected a task item to already exist from the background step");
    }

    [Given(@"I am viewing a task that I have reacted to")]
    public async Task GivenIAmViewingATaskThatIHaveReactedTo()
    {
        var task = _currentTask;
        var reaction = await GetOrCreateFirstReactionAsync();

        _fixture.LastResponse = await _fixture.HttpClient.PostAsync(
            $"/api/tasks/{task!.Id}/reactions/{reaction.Id}", null);
        _fixture.LastResponse.EnsureSuccessStatusCode();

        _selectedReaction = reaction;
    }

    [When(@"I click on the 'Face' icon")]
    public async Task WhenIClickOnTheFaceIcon()
    {
        await EnsureReactionsSeededAsync();

        _fixture.LastResponse = await _fixture.HttpClient.GetAsync("/api/reactions");
    }

    [When(@"I select a reaction")]
    public async Task WhenISelectAReaction()
    {
        var task = _currentTask;
        var reaction = await GetOrCreateFirstReactionAsync();

        _fixture.LastResponse = await _fixture.HttpClient.PostAsync(
            $"/api/tasks/{task!.Id}/reactions/{reaction.Id}", null);

        _selectedReaction = reaction;
    }

    [When(@"I click the highlighted icon of the reaction button")]
    public async Task WhenIClickTheHighlightedIconOfTheReactionButton()
    {
        var task = _currentTask;
        var reaction = _selectedReaction;

        _fixture.LastResponse = await _fixture.HttpClient.PostAsync(
            $"/api/tasks/{task!.Id}/reactions/{reaction!.Id}", null);
        _fixture.LastResponse!.IsSuccessStatusCode.ShouldBeTrue();

        var result = await _fixture.LastResponse.Content.ReadFromJsonAsync<ReactionResponse>();
        result.ShouldNotBeNull();
        _toggleResult = result!;
    }

    [Then(@"I can React to that task with an icon selected from a predefined set of icons")]
    public async Task ThenICanReactToThatTaskWithAnIconFromAPredefinedSet()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.IsSuccessStatusCode.ShouldBeTrue();

        var reactions = await _fixture.LastResponse.Content.ReadFromJsonAsync<List<Reaction>>();
        reactions.ShouldNotBeNull();
        reactions.ShouldNotBeEmpty();
    }

    [Then(@"the reaction icon is shown on the task with an updated count of the number of same reactions used shown next to it")]
    public async Task ThenTheReactionIconIsShownWithAnUpdatedCount()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.IsSuccessStatusCode.ShouldBeTrue();

        var expectedReaction = _selectedReaction;
        var result = await _fixture.LastResponse.Content.ReadFromJsonAsync<ReactionResponse>();

        result.ShouldNotBeNull();
        result!.Reaction.Id.ShouldBe(expectedReaction!.Id);
        result.Count.ShouldBe(1);
        result.ReactedByCurrentUser.ShouldBeTrue();
    }

    [Then(@"the highlight on the reaction button is removed")]
    public void ThenTheHighlightOnTheReactionButtonIsRemoved()
    {
        var result = _toggleResult;
        result!.ReactedByCurrentUser.ShouldBeFalse();
    }

    [Then(@"the number of reactions on that public task is decremented by 1")]
    public void ThenTheNumberOfReactionsIsDecrementedBy1()
    {
        var result = _toggleResult;
        result!.Count.ShouldBe(0);
    }

    private async Task EnsureReactionsSeededAsync()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        if (dbContext.Reactions.Any())
        {
            return;
        }

        dbContext.Reactions.AddRange(
            new Reaction { Name = "thumbsup", FileName = "thumbsup.png" },
            new Reaction { Name = "heart", FileName = "heart.png" },
            new Reaction { Name = "laugh", FileName = "laugh.png" }
        );
        await dbContext.SaveChangesAsync();
    }

    private async Task<Reaction> GetOrCreateFirstReactionAsync()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var existing = dbContext.Reactions.FirstOrDefault();
        if (existing != null)
        {
            return existing;
        }

        var reaction = new Reaction { Name = "thumbsup", FileName = "thumbsup.png" };
        dbContext.Reactions.Add(reaction);
        await dbContext.SaveChangesAsync();
        return reaction;
    }

    private async Task<int> GetTestUserIdAsync()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var userId = _fixture.CurrentUserId;
        var user = dbContext.Users.FirstOrDefault(u => u.Id == userId);
        user.ShouldNotBeNull("Expected the test user to already exist after LoginAsTestUser()");
        return user!.Id;
    }
}