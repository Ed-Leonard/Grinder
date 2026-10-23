using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Grinder.Api.Models.Entities;
using Grinder.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Controllers;

public class DirectMessageControllerTest : BaseIntegrationTestFixture
{
	public DirectMessageControllerTest(WebApplicationFactory<Program> webAppFactory)
		: base(webAppFactory, handleCookies: true) { }

	private User _user = null!;

	private async Task RegisterAndLogin()
	{
		var existingUser = new User
		{
			Email = "test@test.com",
			DisplayName = "Test User",
			PasswordHash = "Password6767$",
			Country = "NZ",
			IsVerified = true
		};

		var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
		existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, "Password6767$");

		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		dbContext.Users.Add(existingUser);
		await dbContext.SaveChangesAsync();

		var loginUserRequest = new LoginRequest("test@test.com", "Password6767$");
		await HttpClient.PostAsJsonAsync("/api/auth/login", loginUserRequest);
	}

	private async Task<User> CreateUserAsync(string email, string displayName)
	{
		var user = new User
		{
			Email = email,
			DisplayName = displayName,
			PasswordHash = "Password123!",
			Country = "NZ",
			IsVerified = true
		};

		var passwordHasher = ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
		user.PasswordHash = passwordHasher.HashPassword(user, "Password6767$");

		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		dbContext.Users.Add(user);
		await dbContext.SaveChangesAsync();
		return user;
	}

	private async Task<TaskItem> CreateTaskAsync(
	int userId,
	string title,
	TaskVisibility visibility = TaskVisibility.Public)
	{
		var task = new TaskItem
		{
			UserId = userId,
			Title = title,
			Visibility = visibility,
			ItemStatus = TaskItemStatus.Todo,
			DueDate = DateOnly.FromDateTime(TestNow.UtcDateTime.AddDays(7)),
			DueTime = new TimeOnly(23, 59),
			CreatedAt = TestNow,
		};

		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		dbContext.TaskItems.Add(task);
		await dbContext.SaveChangesAsync();
		return task;
	}

	private async Task AddMessageAsync(
		int senderId,
		int receiverId,
		string content,
		bool isRead = false)
	{
		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		dbContext.DirectMessages.Add(new DirectMessage
		{
			SenderId = senderId,
			ReceiverId = receiverId,
			Content = content,
			IsRead = isRead,
			SentAt = TestNow.UtcDateTime
		});
		await dbContext.SaveChangesAsync();
		TestNow = TestNow.AddMinutes(1);
		FakeTimeProvider.SetUtcNow(TestNow);
	}

	private async Task AddMutualFollowAsync(int userId, int otherUserId)
	{
		await using var dbContext = await DbContextFactory.CreateDbContextAsync();
		dbContext.Follow.AddRange(
			new Follow { FollowerId = userId, FollowedId = otherUserId },
			new Follow { FollowerId = otherUserId, FollowedId = userId });
		await dbContext.SaveChangesAsync();
	}

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RegisterAndLogin();

		await using var context = await DbContextFactory.CreateDbContextAsync();
		_user = (await context.Users.FirstOrDefaultAsync(u => u.Email == "test@test.com"))!;
	}

	[Fact]
	public async Task GetConversations_Unauthenticated_ReturnsUnauthorized()
	{
		var response = await UnauthenticatedClient.GetAsync("/api/directmessages/conversations");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetConversations_NoMessages_ReturnsEmptyList()
	{
		var response = await HttpClient.GetAsync("/api/directmessages/conversations");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<List<ConversationPreviewResponse>>();
		result.ShouldNotBeNull();
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetConversations_ReturnsPreviewForCurrentUserOnly()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var alex = await CreateUserAsync("alex@cool.com", "Alex");
		var random = await CreateUserAsync("random@cool.com", "Random");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMutualFollowAsync(_user.Id, alex.Id);

		await AddMessageAsync(jane.Id, _user.Id, "hey", isRead: false);
		await AddMessageAsync(jane.Id, _user.Id, "you up?", isRead: false);

		await AddMessageAsync(_user.Id, alex.Id, "no", isRead: true);
		await AddMessageAsync(random.Id, alex.Id, "should not show", isRead: false);

		var response = await HttpClient.GetAsync("/api/directmessages/conversations");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<List<ConversationPreviewResponse>>();
		result.ShouldNotBeNull();
		result.Count.ShouldBe(2);

		var janePreview = result.Single(p => p.OtherUserId == jane.Id);
		janePreview.OtherUserDisplayName.ShouldBe("Jane");
		janePreview.LastMessagePreview.ShouldBe("you up?");
		janePreview.UnreadCount.ShouldBe(2);
		janePreview.HasUnread.ShouldBeTrue();

		var alexPreview = result.Single(p => p.OtherUserId == alex.Id);
		alexPreview.OtherUserDisplayName.ShouldBe("Alex");
		alexPreview.LastMessagePreview.ShouldBe("no");
		alexPreview.UnreadCount.ShouldBe(0);
		alexPreview.HasUnread.ShouldBeFalse();
	}

	[Fact]
	public async Task GetConversation_WhenNotFriends_ReturnsForbidden()
	{
		var stranger = await CreateUserAsync("stranger@cool.com", "Stranger");
		await AddMessageAsync(stranger.Id, _user.Id, "secret", isRead: false);

		var response = await HttpClient.GetAsync($"/api/directmessages/conversation/{stranger.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task GetConversation_WhenFriends_ReturnsMessagesOk()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMessageAsync(jane.Id, _user.Id, "hey there", isRead: false);

		var response = await HttpClient.GetAsync($"/api/directmessages/conversation/{jane.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var result = await response.Content.ReadFromJsonAsync<ConversationPageResponse>();
		result.ShouldNotBeNull();
		result.Messages.ShouldHaveSingleItem().Content.ShouldBe("hey there");
		result.Page.ShouldBe(1);
	}

	[Fact]
	public async Task GetConversation_FullPage_HasMoreIsTrue()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		for (var i = 0; i < 3; i++)
			await AddMessageAsync(jane.Id, _user.Id, $"msg {i}");

		var response = await HttpClient.GetAsync($"/api/directmessages/conversation/{jane.Id}?page=1&limit=2");

		var result = await response.Content.ReadFromJsonAsync<ConversationPageResponse>();
		result!.HasMore.ShouldBeTrue();
	}

	[Fact]
	public async Task GetConversation_PartialPage_HasMoreIsFalse()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMessageAsync(jane.Id, _user.Id, "only one");

		var response = await HttpClient.GetAsync($"/api/directmessages/conversation/{jane.Id}?page=1&limit=20");

		var result = await response.Content.ReadFromJsonAsync<ConversationPageResponse>();
		result!.HasMore.ShouldBeFalse();
	}

	[Fact]
	public async Task GetConversation_Unauthenticated_ReturnsUnauthorized()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");

		var response = await UnauthenticatedClient.GetAsync($"/api/directmessages/conversation/{jane.Id}");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetConversation_PageQueryParam_ReturnsCorrectPage()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		for (var i = 1; i <= 3; i++)
			await AddMessageAsync(jane.Id, _user.Id, $"msg {i}");

		var response = await HttpClient.GetAsync($"/api/directmessages/conversation/{jane.Id}?page=2&limit=2");

		var result = await response.Content.ReadFromJsonAsync<ConversationPageResponse>();
		result!.Messages.ShouldHaveSingleItem().Content.ShouldBe("msg 1");
	}

	[Fact]
	public async Task GetShareableFriends_Unauthenticated_ReturnsUnauthorized()
	{
		var response = await UnauthenticatedClient.GetAsync("/api/directmessages/1/shareable-friends");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task GetShareableFriends_TaskNotFound_ReturnsBadRequest()
	{
		var response = await HttpClient.GetAsync("/api/directmessages/999/shareable-friends");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GetShareableFriends_OwnPublicTask_ReturnsAllFriends()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var alex = await CreateUserAsync("alex@cool.com", "Alex");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMutualFollowAsync(_user.Id, alex.Id);

		var task = await CreateTaskAsync(_user.Id, "My public task", TaskVisibility.Public);

		var response = await HttpClient.GetAsync($"/api/directmessages/{task.Id}/shareable-friends");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<List<FriendResponse>>();
		result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result.ShouldContain(f => f.Id == jane.Id);
		result.ShouldContain(f => f.Id == alex.Id);
	}

	[Fact]
	public async Task GetShareableFriends_OwnTask_ExcludesFriendsAlreadySharedWith()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var alex = await CreateUserAsync("alex@cool.com", "Alex");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMutualFollowAsync(_user.Id, alex.Id);

		var task = await CreateTaskAsync(_user.Id, "My task");

		await HttpClient.PostAsJsonAsync("/api/directmessages/share-task",
			new { TaskId = task.Id, ReceiverIds = new[] { jane.Id } });

		var response = await HttpClient.GetAsync($"/api/directmessages/{task.Id}/shareable-friends");
		var result = await response.Content.ReadFromJsonAsync<List<FriendResponse>>();

		result.ShouldNotBeNull();
		result.ShouldHaveSingleItem();
		result.Single().Id.ShouldBe(alex.Id);
	}

	[Fact]
	public async Task GetShareableFriends_OthersPrivateTask_ReturnsBadRequest()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's private task", TaskVisibility.Private);

		var response = await HttpClient.GetAsync($"/api/directmessages/{task.Id}/shareable-friends");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task GetShareableFriends_OthersFriendsOnlyTask_ReturnsMutualFriendsOnly()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var alex = await CreateUserAsync("alex@cool.com", "Alex"); // friend of caller, not of jane
		var sam = await CreateUserAsync("sam@cool.com", "Sam");    // friend of both

		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMutualFollowAsync(_user.Id, alex.Id);
		await AddMutualFollowAsync(_user.Id, sam.Id);
		await AddMutualFollowAsync(jane.Id, sam.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's friends-only task", TaskVisibility.FriendsOnly);

		var response = await HttpClient.GetAsync($"/api/directmessages/{task.Id}/shareable-friends");
		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<List<FriendResponse>>();
		result.ShouldNotBeNull();
		result.ShouldHaveSingleItem();
		result.Single().Id.ShouldBe(sam.Id);
	}

	[Fact]
	public async Task GetShareableFriends_OthersFriendsOnlyTask_CallerNotFriendOfOwner_ReturnsBadRequest()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var task = await CreateTaskAsync(jane.Id, "Jane's friends-only task", TaskVisibility.FriendsOnly);

		var response = await HttpClient.GetAsync($"/api/directmessages/{task.Id}/shareable-friends");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ShareTask_Unauthenticated_ReturnsUnauthorized()
	{
		var response = await UnauthenticatedClient.PostAsJsonAsync("/api/directmessages/share-task",
			new { TaskId = 1, ReceiverIds = new[] { 1 } });

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ShareTask_TaskNotFound_ReturnsBadRequest()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);

		var response = await HttpClient.PostAsJsonAsync("/api/directmessages/share-task",
			new { TaskId = 999, ReceiverIds = new[] { jane.Id } });

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task ShareTask_ValidFriends_ReturnsCreatedMessages()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var alex = await CreateUserAsync("alex@cool.com", "Alex");
		await AddMutualFollowAsync(_user.Id, jane.Id);
		await AddMutualFollowAsync(_user.Id, alex.Id);

		var task = await CreateTaskAsync(_user.Id, "Shared task");

		var response = await HttpClient.PostAsJsonAsync("/api/directmessages/share-task",
			new { TaskId = task.Id, ReceiverIds = new[] { jane.Id, alex.Id } });

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<List<DirectMessageResponse>>();
		result.ShouldNotBeNull();
		result.Count.ShouldBe(2);
		result.ShouldContain(m => m.ReceiverId == jane.Id && m.SharedTaskId == task.Id);
		result.ShouldContain(m => m.ReceiverId == alex.Id && m.SharedTaskId == task.Id);
	}

	[Fact]
	public async Task ShareTask_IneligibleReceiver_IsSilentlyDropped()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		var stranger = await CreateUserAsync("stranger@cool.com", "Stranger");
		await AddMutualFollowAsync(_user.Id, jane.Id);

		var task = await CreateTaskAsync(_user.Id, "Shared task");

		var response = await HttpClient.PostAsJsonAsync("/api/directmessages/share-task",
			new { TaskId = task.Id, ReceiverIds = new[] { jane.Id, stranger.Id } });

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var result = await response.Content.ReadFromJsonAsync<List<DirectMessageResponse>>();
		result.ShouldNotBeNull();
		result.ShouldHaveSingleItem();
		result.Single().ReceiverId.ShouldBe(jane.Id);
	}

	[Fact]
	public async Task ShareTask_OthersPrivateTask_ReturnsBadRequest()
	{
		var jane = await CreateUserAsync("jane@cool.com", "Jane");
		await AddMutualFollowAsync(_user.Id, jane.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's private task", TaskVisibility.Private);

		var response = await HttpClient.PostAsJsonAsync("/api/directmessages/share-task",
			new { TaskId = task.Id, ReceiverIds = new[] { jane.Id } });

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

}
