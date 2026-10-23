using System.Net;
using System.Net.Http.Json;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Tests.Acceptance.Setup;
using Reqnroll;
using Shouldly;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U30012ExploreSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private HttpResponseMessage? _response;
    private ExplorePage? _explorePage;
    private SearchResponseDto? _searchResults;
    private int[] _expectedOrder = Array.Empty<int>();
    private string _searchQuery = string.Empty;
    private int _expectedTaskId;
    private const int ExplorePageSize = 20;
    private int[] _firstPageIds = Array.Empty<int>();
    private int[] _secondPageIds = Array.Empty<int>();
    private HttpResponseMessage? _secondPageResponse;

    public U30012ExploreSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }


    [Given("there are existing tasks")]
    public async Task GivenThereAreExistingTasks()
    {

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();

        var owner = new User
        {
            Email = "owner-ac1@example.com",
            DisplayName = "Marion",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = _fixture.AcceptanceTestNow.DateTime
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var reaction = new Reaction { Name = "reaction", FileName = "reaction.png" };
        context.Reactions.Add(reaction);
        await context.SaveChangesAsync();

        var reactors = Enumerable.Range(1, 5)
            .Select(i => new User
            {
                Email = $"reactor-ac1-{i}@example.com",
                DisplayName = $"Reactor{i}",
                PasswordHash = "not-a-real-hash",
                Country = "NZ",
                CreatedAt = _fixture.AcceptanceTestNow.DateTime
            })
            .ToList();
        context.Users.AddRange(reactors);
        await context.SaveChangesAsync();

        var unseenHigh = NewPublicTask(owner.Id, "Unseen, high trend score");
        var unseenLow = NewPublicTask(owner.Id, "Unseen, low trend score");
        var seenHigh = NewPublicTask(owner.Id, "Seen, high trend score");
        var seenLow = NewPublicTask(owner.Id, "Seen, low trend score");

        context.TaskItems.AddRange(unseenHigh, unseenLow, seenHigh, seenLow);
        await context.SaveChangesAsync();

        var reactorIds = reactors.Select(r => r.Id).ToList();
        AddReactions(context, unseenHigh.Id, reaction.Id, reactorIds.Take(5));
        AddReactions(context, unseenLow.Id, reaction.Id, reactorIds.Take(1));
        AddReactions(context, seenHigh.Id, reaction.Id, reactorIds.Take(5));
        AddReactions(context, seenLow.Id, reaction.Id, reactorIds.Take(1));
        await context.SaveChangesAsync();

        context.TaskViews.AddRange(
            new TaskView { UserId = _fixture.CurrentUserId!.Value, TaskItemId = seenHigh.Id, ViewedAt = _fixture.AcceptanceTestNow },
            new TaskView { UserId = _fixture.CurrentUserId!.Value, TaskItemId = seenLow.Id, ViewedAt = _fixture.AcceptanceTestNow });
        await context.SaveChangesAsync();

        _expectedOrder = new[] { unseenHigh.Id, unseenLow.Id, seenHigh.Id, seenLow.Id };
    }

    [When("I load the explore page")]
    public async Task WhenILoadTheExplorePage()
    {
        _response = await _fixture.HttpClient.GetAsync("api/Explore?page=1&limit=20");
        _explorePage = await _response.Content.ReadFromJsonAsync<ExplorePage>();
    }

    [Then(@"the feed shows unseen trending content first, then seen trending content")]
    public void ThenAFeedIsShownGroupedByTrending()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _explorePage.ShouldNotBeNull();

        _explorePage.Items.Select(i => i.Id).ShouldBe(_expectedOrder);
    }

    [Given("there is at least one item matching the query {string}")]
    public async Task GivenThereIsAtLeastOneItemMatchingTheQuery(string query)
    {
        _searchQuery = query;

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();

        var owner = new User
        {
            Email = "owner-ac2@example.com",
            DisplayName = "Sallah",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = _fixture.AcceptanceTestNow.DateTime
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var titleMatch = NewPublicTask(owner.Id, $"{_searchQuery} launch plan");
        context.TaskItems.Add(titleMatch);
        var titleMatch2 = NewPublicTask(owner.Id, $"{_searchQuery} launch plan");
        context.TaskItems.Add(titleMatch2);
        var descriptionMatch1 = NewPublicTask(owner.Id, "Title", $"{_searchQuery}");
        context.TaskItems.Add(descriptionMatch1);
        var descriptionMatch2 = NewPublicTask(owner.Id, "Title", $"{_searchQuery} and more");
        context.TaskItems.Add(descriptionMatch2);

        var reaction = new Reaction { Name = "reaction", FileName = "reaction.png" };
        context.Reactions.Add(reaction);
        await context.SaveChangesAsync();
        AddReactions(context, titleMatch.Id, reaction.Id, [owner.Id]);
        await context.SaveChangesAsync();

        _expectedOrder = [titleMatch.Id, titleMatch2.Id, descriptionMatch1.Id, descriptionMatch2.Id];
    }

    [When("I search for {string}")]
    public async Task WhenISearchFor(string query)
    {
        _response = await _fixture.HttpClient.GetAsync($"api/Search?query={Uri.EscapeDataString(query)}");
        _searchResults = await _response.Content.ReadFromJsonAsync<SearchResponseDto>();
    }

    [Then(@"the results are ordered by match strength, then by interaction count")]
    public void ThenAPageOfMatchingContentItemsPopulatesTheExploreFeed()
    {
        _response.ShouldNotBeNull();
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _searchResults.ShouldNotBeNull();
        _searchResults.Tasks.Select(i => i.Id).ShouldBe(_expectedOrder);
    }


    private TaskItem NewPublicTask(int ownerId, string title, string description = "") => new()
    {
        UserId = ownerId,
        Title = title,
        Description = description,
        Visibility = TaskVisibility.Public,
        CreatedAt = _fixture.AcceptanceTestNow,
        UpdatedAt = _fixture.AcceptanceTestNow,
        DueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.AddDays(7).DateTime)
    };

    private void AddReactions(DataAccess.DatabaseContext context, int taskId, int reactionId, IEnumerable<int> reactingUserIds)
    {
        foreach (var userId in reactingUserIds)
        {
            context.TaskReactions.Add(new TaskReaction
            {
                TaskItemId = taskId,
                UserId = userId,
                ReactionId = reactionId,
                CreatedAt = _fixture.AcceptanceTestNow
            });
        }
    }

    [Given("there are more tasks than fit on a single page")]
    public async Task GivenThereAreMoreTasksThanFitOnASinglePage()
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();

        var owner = new User
        {
            Email = "owner-ac3@example.com",
            DisplayName = "Willie",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = _fixture.AcceptanceTestNow.DateTime
        };
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        // More tasks than ExplorePageSize guarantees a non-empty second page.
        var tasks = Enumerable.Range(1, ExplorePageSize + 5)
            .Select(i => NewPublicTask(owner.Id, $"Pagination task {i}"))
            .ToList();
        context.TaskItems.AddRange(tasks);
        await context.SaveChangesAsync();
    }

    [When(@"I load page (\d+) of the explore feed")]
    public async Task WhenILoadPageOfTheExploreFeed(int pageNumber)
    {
        var response = await _fixture.HttpClient.GetAsync($"api/Explore?page={pageNumber}&limit={ExplorePageSize}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ExplorePage>();
        page.ShouldNotBeNull();

        if (pageNumber == 1)
        {
            _response = response;
            _explorePage = page;
            _firstPageIds = page.Items.Select(i => i.Id).ToArray();
        }
        else
        {
            _secondPageResponse = response;
            _secondPageIds = page.Items.Select(i => i.Id).ToArray();
        }
    }

    [Then("the second page contains no items from the first page")]
    public void ThenTheSecondPageContainsNoItemsFromTheFirstPage()
    {
        _secondPageResponse.ShouldNotBeNull();
        _secondPageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        _secondPageIds.ShouldNotBeEmpty();

        var overlap = _secondPageIds.Intersect(_firstPageIds).ToArray();
        overlap.ShouldBeEmpty();
    }

    [When("I load pages of the explore feed until no more content is available")]
    public async Task WhenILoadPagesUntilNoMoreContentIsAvailable()
    {
        var page = 1;
        ExplorePage? lastPage;
        const int maxPagesToPreventInfiniteLoop = 50;

        do
        {
            _response = await _fixture.HttpClient.GetAsync($"api/Explore?page={page}&limit={ExplorePageSize}");
            _response.StatusCode.ShouldBe(HttpStatusCode.OK);
            lastPage = await _response.Content.ReadFromJsonAsync<ExplorePage>();
            lastPage.ShouldNotBeNull();
            page++;
        }
        while (lastPage.Items.Count == ExplorePageSize && page <= maxPagesToPreventInfiniteLoop);

        _explorePage = lastPage;
    }

    [Then("the final page indicates no more content is available")]
    public void ThenTheFinalPageIndicatesNoMoreContentIsAvailable()
    {
        _explorePage.ShouldNotBeNull();
        _explorePage.Items.Count.ShouldBeLessThan(ExplorePageSize);
    }
}
