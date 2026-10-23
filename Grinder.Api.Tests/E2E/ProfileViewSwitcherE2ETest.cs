using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Shouldly;

namespace Grinder.Api.Tests.E2E;

public class ProfileViewSwitcherE2ETest : E2ETestFixture
{
	public ProfileViewSwitcherE2ETest(WebApplicationFactory<Program> factory) : base(factory)
	{
	}

	[Fact]
	public async Task ShouldSwitchBetweenFeedAndTasksViewOnOwnProfile()
	{
		await LoginViaUI("jane@example.com");


		await Page.Locator("[data-testid='feed-view-pane']").WaitForAsync();
		(await Page.Locator("[data-testid='feed-view-pane']").GetAttributeAsync("class"))
			.ShouldNotContain("opacity-0");
		(await Page.Locator("[data-testid='tasks-view-pane']").GetAttributeAsync("class"))
			.ShouldContain("opacity-0");

		// Switch to the tasks view
		await Page.Locator("[data-testid='tasks-view-button']").ClickAsync();

		(await Page.Locator("[data-testid='tasks-view-pane']").GetAttributeAsync("class"))
			.ShouldNotContain("opacity-0");
		(await Page.Locator("[data-testid='feed-view-pane']").GetAttributeAsync("class"))
			.ShouldContain("opacity-0");

		// Task that is visible in the tasks view
		await Page.Locator("[data-testid='tasks-view-pane']").GetByText("Get milk").WaitForAsync();

		// Switch back to feed view
		await Page.Locator("[data-testid='feed-view-button']").ClickAsync();

		(await Page.Locator("[data-testid='feed-view-pane']").GetAttributeAsync("class"))
			.ShouldNotContain("opacity-0");
		(await Page.Locator("[data-testid='tasks-view-pane']").GetAttributeAsync("class"))
			.ShouldContain("opacity-0");
	}

	[Fact]
	public async Task ShouldSwitchToCalendarViewOnOwnProfile()
	{
		await LoginViaUI("jane@example.com");

		await Page.Locator("[data-testid='feed-view-pane']").WaitForAsync();
		(await Page.Locator("[data-testid='calendar-view-pane']").CountAsync()).ShouldBe(0);

		// Switch to the calendar view
		await Page.Locator("[data-testid='calendar-view-button']").ClickAsync();

		await Page.Locator("[data-testid='calendar-view-pane']").WaitForAsync();
		(await Page.Locator("[data-testid='calendar-view-pane']").CountAsync()).ShouldBe(1);
	}

	[Fact]
	public async Task ShouldHideTasksViewAndShowTheirFeedWhenViewingAnotherUsersProfile()
	{
		await LoginViaUI("jane@example.com");

		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		var john = await dbContext.Users.SingleAsync(u => u.Email == "john@example.com");

		await Page.GotoAsync($"http://localhost/profile/{john.Id}");

		// John's display name is shown
		await Page.Locator("h1:has-text('John')").WaitForAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Unfollow" }).WaitForAsync();

		// Feed button is labelled with John's name, since this isn't Jane's own profile
		await Page.Locator("[data-testid='feed-view-button']:has-text(\"John's Feed\")").WaitForAsync();

		// The "My Tasks" view is only visible on your own profile
		(await Page.Locator("[data-testid='tasks-view-button']").CountAsync()).ShouldBe(0);

		await Page.Locator("[data-testid='feed-view-pane']").GetByText("Adopt every single cat").WaitForAsync();
	}

	[Fact]
	public async Task ShouldNavigateToAFriendsProfileFromTheFriendsList()
	{
		await LoginViaUI("jane@example.com");

		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		var jane = await dbContext.Users.SingleAsync(u => u.Email == "jane@example.com");

		await Page.GotoAsync($"http://localhost/friends/{jane.Id}");

		// Selecting a friend's name takes me to their profile page
		await Page.GetByRole(AriaRole.Link, new() { Name = "John" }).ClickAsync();

		await Page.WaitForURLAsync("**/profile/*");
		await Page.Locator("h1:has-text('John')").WaitForAsync();
	}
}
