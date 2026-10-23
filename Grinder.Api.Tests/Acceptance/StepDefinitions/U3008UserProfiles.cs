using Reqnroll;
using Grinder.Api.Tests.Acceptance.Setup;
using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U3008UserProfilesSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private HttpResponseMessage? _response;
    private List<BadgeResponse>? _badges;
    private int? _otherUserId;
    private PublicProfileResponse? _publicProfile;
    private List<TaskResponse>? _visibleTasks;
    private StatusCountResponse? _statusCounts;

    public U3008UserProfilesSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task EnsureBadgesSeededAsync(DatabaseContext dbContext)
    {
        if (await dbContext.Badges.AnyAsync()) return;

        dbContext.Badges.AddRange(
            new Badge
            {
                Name = "The Grinder",
                Description = "Awarded for moving 10 tasks into Done status",
                Icon = "grinder",
                CriteriaKey = BadgeCriteriaKeys.TasksDone,
                Threshold = 10
            },
            new Badge
            {
                Name = "Flawless",
                Description = "Awarded for creating 25 tasks",
                Icon = "stars",
                CriteriaKey = BadgeCriteriaKeys.TasksCreated,
                Threshold = 25
            }
        );
        await dbContext.SaveChangesAsync();
    }

    [Given(@"I have earned a badge")]
    public async Task GivenIHaveEarnedABadge()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        await EnsureBadgesSeededAsync(dbContext);

        // Create enough done tasks to earn "The Grinder" badge
        var userId = _fixture.CurrentUserId!.Value;
        for (var i = 0; i < 10; i++)
        {
            dbContext.TaskItems.Add(new TaskItem
            {
                Title = $"Done Task {i + 1}",
                UserId = userId,
                ItemStatus = TaskItemStatus.Done
            });
        }
        await dbContext.SaveChangesAsync();
    }

    [Given(@"I have earned the badge ""(.*)""")]
    public async Task GivenIHaveEarnedTheBadge(string badgeName)
    {
        if (badgeName != "The Grinder")
        {
            throw new NotSupportedException("Only \"The Grinder\" can be earned by this step (via 10 done tasks).");
        }

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        await EnsureBadgesSeededAsync(dbContext);

        var userId = _fixture.CurrentUserId!.Value;
        for (var i = 0; i < 10; i++)
        {
            dbContext.TaskItems.Add(new TaskItem
            {
                Title = $"Done Task {i + 1}",
                UserId = userId,
                ItemStatus = TaskItemStatus.Done
            });
        }
        await dbContext.SaveChangesAsync();

        // Trigger evaluation so the badge is actually recorded as earned.
        var response = await _fixture.HttpClient.GetAsync("api/badge");
        response.EnsureSuccessStatusCode();

        var badge = await dbContext.Badges.SingleAsync(b => b.Name == badgeName);
        var earned = await dbContext.UserBadges.AnyAsync(ub => ub.UserId == userId && ub.BadgeId == badge.Id);
        earned.ShouldBeTrue($"Expected badge '{badgeName}' to have been earned.");
    }

    [Given(@"the badge ""(.*)"" exists and I have not earned it")]
    public async Task GivenTheBadgeExistsAndIHaveNotEarnedIt(string badgeName)
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        await EnsureBadgesSeededAsync(dbContext);
        (await dbContext.Badges.AnyAsync(b => b.Name == badgeName)).ShouldBeTrue();
    }

    [When(@"I navigate to the Badges page")]
    public async Task WhenINavigateToTheBadgesPage()
    {
        // Seed badges if not already present (for scenarios that don't call the Given step)
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        await EnsureBadgesSeededAsync(dbContext);

        _response = await _fixture.HttpClient.GetAsync("api/badge");
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _badges = await _response.Content.ReadFromJsonAsync<List<BadgeResponse>>();
    }

    [When(@"I request my badges for selection")]
    public async Task WhenIRequestMyBadgesForSelection()
    {
        _response = await _fixture.HttpClient.GetAsync("api/badge");
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _badges = await _response.Content.ReadFromJsonAsync<List<BadgeResponse>>();
    }

    [Then(@"""(.*)"" is offered as selectable")]
    public void ThenBadgeIsOfferedAsSelectable(string badgeName)
    {
        _badges.ShouldNotBeNull();
        var badge = _badges!.SingleOrDefault(b => b.Name == badgeName);
        badge.ShouldNotBeNull($"Expected badge '{badgeName}' to be returned");
        badge!.IsUnlocked.ShouldBeTrue();
    }

    [Then(@"""(.*)"" is not offered as selectable")]
    public void ThenBadgeIsNotOfferedAsSelectable(string badgeName)
    {
        _badges.ShouldNotBeNull();
        var badge = _badges!.SingleOrDefault(b => b.Name == badgeName);
        badge.ShouldNotBeNull($"Expected badge '{badgeName}' to be returned");
        badge!.IsUnlocked.ShouldBeFalse();
    }

    [When(@"I select the badge ""(.*)""")]
    public async Task WhenISelectTheBadge(string badgeName)
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var badge = await dbContext.Badges.SingleAsync(b => b.Name == badgeName);

        _response = await _fixture.HttpClient.PutAsJsonAsync("api/badge/select", new SelectBadgeRequest(badge.Id));
    }

    [Given(@"I have selected the badge ""(.*)""")]
    public async Task GivenIHaveSelectedTheBadge(string badgeName)
    {
        await WhenISelectTheBadge(badgeName);
        _response!.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [When(@"I select ""No Status""")]
    public async Task WhenISelectNoStatus()
    {
        _response = await _fixture.HttpClient.PutAsJsonAsync("api/badge/select", new SelectBadgeRequest(null));
    }

    [Then(@"the selection succeeds")]
    public void ThenTheSelectionSucceeds()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Then(@"the selection is rejected")]
    public void ThenTheSelectionIsRejected()
    {
        _response.ShouldNotBeNull();
        _response!.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Then(@"my public profile shows the badge ""(.*)""")]
    public async Task ThenMyPublicProfileShowsTheBadge(string badgeName)
    {
        var userId = _fixture.CurrentUserId!.Value;
        var response = await _fixture.HttpClient.GetAsync($"api/user/{userId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.SelectedBadgeName.ShouldBe(badgeName);
    }

    [Then(@"my public profile shows no badge")]
    public async Task ThenMyPublicProfileShowsNoBadge()
    {
        var userId = _fixture.CurrentUserId!.Value;
        var response = await _fixture.HttpClient.GetAsync($"api/user/{userId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<PublicProfileResponse>();
        profile.ShouldNotBeNull();
        profile!.SelectedBadgeName.ShouldBeNull();
    }

    [Given(@"another user exists")]
    public async Task GivenAnotherUserExists()
    {
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        var otherUser = new User
        {
            Email = $"other-{Guid.NewGuid():N}@test.com",
            DisplayName = "Other User",
            PasswordHash = "hashedpassword",
            Country = "NZ",
            IsVerified = true
        };
        dbContext.Users.Add(otherUser);
        await dbContext.SaveChangesAsync();
        _otherUserId = otherUser.Id;
    }

    [Given(@"our relationship is ""(.*)""")]
    public async Task GivenOurRelationshipIs(string relationship)
    {
        var currentUserId = _fixture.CurrentUserId!.Value;
        var otherUserId = _otherUserId!.Value;

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();

        switch (relationship)
        {
            case "Follow":
                break;
            case "Following":
                dbContext.Follow.Add(new Follow { FollowerId = currentUserId, FollowedId = otherUserId });
                break;
            case "FollowBack":
                dbContext.Follow.Add(new Follow { FollowerId = otherUserId, FollowedId = currentUserId });
                break;
            case "Friends":
                dbContext.Follow.Add(new Follow { FollowerId = currentUserId, FollowedId = otherUserId });
                dbContext.Follow.Add(new Follow { FollowerId = otherUserId, FollowedId = currentUserId });
                break;
            default:
                throw new ArgumentException($"Unknown relationship: {relationship}");
        }

        await dbContext.SaveChangesAsync();
    }

    [Given(@"we are friends")]
    public async Task GivenWeAreFriends() => await GivenOurRelationshipIs("Friends");

    [Given(@"they have a public task, a friends-only task, and a private task")]
    public async Task GivenTheyHaveTasksOfEachVisibility() =>
        await CreateTasksOfEachVisibilityAsync(_otherUserId!.Value);

    [Given(@"I have a public task, a friends-only task, and a private task")]
    public async Task GivenIHaveTasksOfEachVisibility() =>
        await CreateTasksOfEachVisibilityAsync(_fixture.CurrentUserId!.Value);

    private async Task CreateTasksOfEachVisibilityAsync(int userId)
    {
        var dueDate = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5));

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        dbContext.TaskItems.AddRange(
            new TaskItem { Title = "Public Task", UserId = userId, Visibility = TaskVisibility.Public, ItemStatus = TaskItemStatus.Todo, DueDate = dueDate },
            new TaskItem { Title = "Friends Only Task", UserId = userId, Visibility = TaskVisibility.FriendsOnly, ItemStatus = TaskItemStatus.Todo, DueDate = dueDate },
            new TaskItem { Title = "Private Task", UserId = userId, Visibility = TaskVisibility.Private, ItemStatus = TaskItemStatus.Todo, DueDate = dueDate }
        );
        await dbContext.SaveChangesAsync();
    }

    [Given(@"I have tasks due on ""(.*)"", ""(.*)"", and ""(.*)""")]
    public async Task GivenIHaveTasksDueOn(string due1, string due2, string due3)
    {
        var userId = _fixture.CurrentUserId!.Value;
        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        dbContext.TaskItems.AddRange(
            new TaskItem { Title = $"Task due {due1}", UserId = userId, DueDate = DateOnly.Parse(due1), ItemStatus = TaskItemStatus.Todo, Visibility = TaskVisibility.Private },
            new TaskItem { Title = $"Task due {due2}", UserId = userId, DueDate = DateOnly.Parse(due2), ItemStatus = TaskItemStatus.Todo, Visibility = TaskVisibility.Private },
            new TaskItem { Title = $"Task due {due3}", UserId = userId, DueDate = DateOnly.Parse(due3), ItemStatus = TaskItemStatus.Todo, Visibility = TaskVisibility.Private }
        );
        await dbContext.SaveChangesAsync();
    }

    [Given(@"I have one todo task, one in progress task, one done task, and one overdue task")]
    public async Task GivenIHaveOneTaskOfEachStatus()
    {
        var userId = _fixture.CurrentUserId!.Value;
        var future = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(5));
        var past = DateOnly.FromDateTime(_fixture.AcceptanceTestNow.DateTime.AddDays(-5));

        await using var dbContext = await _fixture.DbContextFactory.CreateDbContextAsync();
        dbContext.TaskItems.AddRange(
            new TaskItem { Title = "Todo Task", UserId = userId, ItemStatus = TaskItemStatus.Todo, DueDate = future, Visibility = TaskVisibility.Private },
            new TaskItem { Title = "In Progress Task", UserId = userId, ItemStatus = TaskItemStatus.InProgress, DueDate = future, Visibility = TaskVisibility.Private },
            new TaskItem { Title = "Done Task", UserId = userId, ItemStatus = TaskItemStatus.Done, DueDate = future, Visibility = TaskVisibility.Private },
            new TaskItem { Title = "Overdue Task", UserId = userId, ItemStatus = TaskItemStatus.Todo, DueDate = past, Visibility = TaskVisibility.Private }
        );
        await dbContext.SaveChangesAsync();
    }

    [When(@"I view their profile")]
    public async Task WhenIViewTheirProfile()
    {
        _response = await _fixture.HttpClient.GetAsync($"api/user/{_otherUserId}");
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _publicProfile = await _response.Content.ReadFromJsonAsync<PublicProfileResponse>();
    }

    [When(@"I view my own profile")]
    public async Task WhenIViewMyOwnProfile()
    {
        var userId = _fixture.CurrentUserId!.Value;
        _response = await _fixture.HttpClient.GetAsync($"api/user/{userId}");
        _response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _publicProfile = await _response.Content.ReadFromJsonAsync<PublicProfileResponse>();
    }

    [Then(@"I see the relationship status ""(.*)""")]
    public void ThenISeeTheRelationshipStatus(string expected)
    {
        _publicProfile.ShouldNotBeNull();
        _publicProfile!.RelationshipStatus.ToString().ShouldBe(expected);
    }

    [Then(@"I see my display name")]
    public void ThenISeeMyDisplayName()
    {
        _publicProfile.ShouldNotBeNull();
        _publicProfile!.DisplayName.ShouldNotBeNullOrWhiteSpace();
    }

    [Then(@"I see my friend count")]
    public void ThenISeeMyFriendCount()
    {
        _publicProfile.ShouldNotBeNull();
        _publicProfile!.FriendCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Then(@"the task grid contains my task")]
    public async Task ThenTheTaskGridContainsMyTask()
    {
        var userId = _fixture.CurrentUserId!.Value;
        var response = await _fixture.HttpClient.GetAsync($"api/tasks/user/{userId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>();
        tasks.ShouldNotBeNull();
        tasks!.ShouldContain(t => t.Title == "Test Task");
    }

    [When(@"I view their task grid")]
    public async Task WhenIViewTheirTaskGrid()
    {
        var response = await _fixture.HttpClient.GetAsync($"api/tasks/user/{_otherUserId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _visibleTasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>();
    }

    [When(@"I view my own task grid")]
    public async Task WhenIViewMyOwnTaskGrid()
    {
        var userId = _fixture.CurrentUserId!.Value;
        var response = await _fixture.HttpClient.GetAsync($"api/tasks/user/{userId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _visibleTasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>();
    }

    [Then(@"I only see their public task")]
    public void ThenIOnlySeeTheirPublicTask()
    {
        _visibleTasks.ShouldNotBeNull();
        _visibleTasks!.Select(t => t.Title).ShouldBe(new[] { "Public Task" });
    }

    [Then(@"I see their public task and their friends-only task")]
    public void ThenISeeTheirPublicAndFriendsOnlyTask()
    {
        _visibleTasks.ShouldNotBeNull();
        var titles = _visibleTasks!.Select(t => t.Title).ToList();
        titles.ShouldContain("Public Task");
        titles.ShouldContain("Friends Only Task");
    }

    [Then(@"I do not see their private task")]
    public void ThenIDoNotSeeTheirPrivateTask()
    {
        _visibleTasks.ShouldNotBeNull();
        _visibleTasks!.ShouldNotContain(t => t.Title == "Private Task");
    }

    [Then(@"I see all 3 of my tasks")]
    public void ThenISeeAll3OfMyTasks()
    {
        _visibleTasks.ShouldNotBeNull();
        _visibleTasks!.Count.ShouldBe(3);
    }

    [Then(@"my tasks are returned ordered by closest due date first")]
    public void ThenMyTasksAreOrderedByClosestDueDateFirst()
    {
        _visibleTasks.ShouldNotBeNull();
        var dueDates = _visibleTasks!.Select(t => t.DueDate).ToList();
        dueDates.ShouldBe(dueDates.OrderBy(d => d).ToList());
    }

    [When(@"I view my own progress status counts")]
    public async Task WhenIViewMyOwnProgressStatusCounts()
    {
        var userId = _fixture.CurrentUserId!.Value;
        var response = await _fixture.HttpClient.GetAsync($"api/tasks/status-count/{userId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _statusCounts = await response.Content.ReadFromJsonAsync<StatusCountResponse>();
    }

    [Then(@"the status counts show (\d+) todo, (\d+) in progress, (\d+) done, and (\d+) overdue")]
    public void ThenTheStatusCountsShow(int todo, int inProgress, int done, int overdue)
    {
        _statusCounts.ShouldNotBeNull();
        _statusCounts!.Todo.ShouldBe(todo);
        _statusCounts.InProgress.ShouldBe(inProgress);
        _statusCounts.Done.ShouldBe(done);
        _statusCounts.Overdue.ShouldBe(overdue);
    }

    [Then(@"I see all badges returned")]
    public void ThenISeeAllBadgesReturned()
    {
        _badges.ShouldNotBeNull();
        _badges!.Count.ShouldBeGreaterThan(0);
    }

    [Then(@"each badge has a name, icon, and unlock criteria")]
    public void ThenEachBadgeHasANameIconAndUnlockCriteria()
    {
        _badges.ShouldNotBeNull();
        foreach (var badge in _badges!)
        {
            badge.Name.ShouldNotBeNullOrWhiteSpace();
            badge.Icon.ShouldNotBeNullOrWhiteSpace();
            badge.Description.ShouldNotBeNullOrWhiteSpace();
            badge.Threshold.ShouldBeGreaterThan(0);
        }
    }

    [Then(@"unlocked badges are visually distinguished from locked badges")]
    public void ThenUnlockedBadgesAreVisuallyDistinguishedFromLockedBadges()
    {
        _badges.ShouldNotBeNull();
        var unlocked = _badges!.Where(b => b.IsUnlocked).ToList();
        var locked = _badges!.Where(b => !b.IsUnlocked).ToList();

        unlocked.ShouldNotBeEmpty();
        locked.ShouldNotBeEmpty();

        // Verify the distinction is made via the IsUnlocked flag
        unlocked.ShouldAllBe(b => b.IsUnlocked);
        locked.ShouldAllBe(b => !b.IsUnlocked);
    }

    [Then(@"unlocked badges show when they were earned")]
    public void ThenUnlockedBadgesShowWhenTheyWereEarned()
    {
        _badges.ShouldNotBeNull();
        var unlocked = _badges!.Where(b => b.IsUnlocked).ToList();
        unlocked.ShouldNotBeEmpty();

        foreach (var badge in unlocked)
        {
            badge.EarnedAt.ShouldNotBeNull();
        }
    }

    [Then(@"all badges are shown as locked")]
    public void ThenAllBadgesAreShownAsLocked()
    {
        _badges.ShouldNotBeNull();
        _badges!.ShouldAllBe(b => !b.IsUnlocked);
    }

    [Then(@"no badges have an earned date")]
    public void ThenNoBadgesHaveAnEarnedDate()
    {
        _badges.ShouldNotBeNull();
        foreach (var badge in _badges!)
        {
            badge.EarnedAt.ShouldBeNull();
        }
    }
}
