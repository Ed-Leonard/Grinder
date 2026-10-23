using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class DirectMessageServiceTest : BaseIntegrationTestFixture
{
	private IDirectMessageService ServiceUnderTest =>
		ServiceProvider.GetRequiredService<IDirectMessageService>();

	public DirectMessageServiceTest(WebApplicationFactory<Program> webAppFactory)
		: base(webAppFactory) { }

	private async Task<User> AddUserAsync(string email, string displayName)
	{
		await using var context = await DbContextFactory.CreateDbContextAsync();
		var user = new User
		{
			Email = email,
			DisplayName = displayName,
			PasswordHash = "not-a-real-hash",
			Country = "NZ",
			IsVerified = true,
			CreatedAt = FakeTimeProvider.GetUtcNow().UtcDateTime
		};

		context.Users.Add(user);
		await context.SaveChangesAsync();
		return user;
	}

	private async Task AddMessageAsync(
		int senderId,
		int receiverId,
		string content,
		bool isRead = false)
	{
		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Add(new DirectMessage
		{
			SenderId = senderId,
			ReceiverId = receiverId,
			Content = content,
			IsRead = isRead,
			SentAt = FakeTimeProvider.GetUtcNow().UtcDateTime
		});
		await context.SaveChangesAsync();
		FakeTimeProvider.Advance(TimeSpan.FromMinutes(1));
	}

	private async Task AddMutualFollowAsync(int userId, int otherUserId)
	{
		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.Follow.AddRange(
			new Follow { FollowerId = userId, FollowedId = otherUserId },
			new Follow { FollowerId = otherUserId, FollowedId = userId });
		await context.SaveChangesAsync();
	}

	private async Task AddFollowAsync(int followerId, int followedId)
	{
		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.Follow.Add(new Follow { FollowerId = followerId, FollowedId = followedId });
		await context.SaveChangesAsync();
	}

	private async Task<(User Me, User Jane)> AddFriendsAsync()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);
		return (me, jane);
	}

	private async Task<TaskItem> CreateTaskAsync(
	int userId,
	string title,
	TaskVisibility visibility = TaskVisibility.Public)
	{
		await using var context = await DbContextFactory.CreateDbContextAsync();
		var task = new TaskItem
		{
			UserId = userId,
			Title = title,
			Visibility = visibility,
			ItemStatus = TaskItemStatus.Todo,
			DueDate = DateOnly.FromDateTime(FakeTimeProvider.GetUtcNow().UtcDateTime.AddDays(7)),
			DueTime = new TimeOnly(23, 59),
			CreatedAt = FakeTimeProvider.GetUtcNow(),
		};

		context.TaskItems.Add(task);
		await context.SaveChangesAsync();
		return task;
	}

	[Fact]
	public async Task GetConversations_NoMessages_ReturnsEmptyList()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);

		previews.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetConversationPreviewsAsync_ReturnsLatestMessagesAndUnreadCountPerOtherUser()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var mike = await AddUserAsync("mike@cool.com", "mike");

		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(me.Id, mike.Id);

		await AddMessageAsync(me.Id, mike.Id, "hi mike", isRead: false);
		await AddMessageAsync(jane.Id, me.Id, "old msg from jane", isRead: false);
		await AddMessageAsync(me.Id, jane.Id, "my reply", isRead: true);
		await AddMessageAsync(jane.Id, me.Id, "latest from jane", isRead: false);

		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);
		previews.Count.ShouldBe(2);

		var janePreview = previews[0];
		janePreview.OtherUserId.ShouldBe(jane.Id);
		janePreview.OtherUserDisplayName.ShouldBe("jane");
		janePreview.LastMessagePreview.ShouldBe("latest from jane");
		janePreview.LastMessageSenderId.ShouldBe(jane.Id);
		janePreview.UnreadCount.ShouldBe(2);
		janePreview.HasUnread.ShouldBeTrue();

		var mikePreview = previews[1];
		mikePreview.OtherUserId.ShouldBe(mike.Id);
		mikePreview.OtherUserDisplayName.ShouldBe("mike");
		mikePreview.LastMessagePreview.ShouldBe("hi mike");
		mikePreview.LastMessageSenderId.ShouldBe(me.Id);
		mikePreview.UnreadCount.ShouldBe(0);
		mikePreview.HasUnread.ShouldBeFalse();
	}

	[Fact]
	public async Task GetConversationPreviewsAsync_UnreadRemainsWhenLastMessageIsOwn()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		await AddMessageAsync(jane.Id, me.Id, "unread one", isRead: false);
		await AddMessageAsync(jane.Id, me.Id, "unread two", isRead: false);
		await AddMessageAsync(me.Id, jane.Id, "my latest", isRead: true);

		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);

		var janePreview = previews.ShouldHaveSingleItem();
		janePreview.LastMessagePreview.ShouldBe("my latest");
		janePreview.LastMessageSenderId.ShouldBe(me.Id);
		janePreview.UnreadCount.ShouldBe(2);
		janePreview.HasUnread.ShouldBeTrue();
	}

	[Fact]
	public async Task GetConversationPreviewsAsync_DoesNotIncludeOtherUsersConversations()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var alex = await AddUserAsync("alex@cool.com", "alex");
		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(jane.Id, alex.Id);

		await AddMessageAsync(jane.Id, alex.Id, "not my conversation", isRead: false);
		await AddMessageAsync(me.Id, jane.Id, "mine", isRead: true);

		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);

		var mine = previews.ShouldHaveSingleItem();
		mine.OtherUserId.ShouldBe(jane.Id);
		mine.LastMessagePreview.ShouldBe("mine");
	}

	[Fact]
	public async Task GetConversationPreviewsAsync_TruncatesLongPreview()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		await AddMessageAsync(jane.Id, me.Id, new string('a', 90), isRead: true);

		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);

		previews.ShouldHaveSingleItem().LastMessagePreview.ShouldBe(new string('a', 80) + "...");
	}

	[Fact]
	public async Task GetConversationPreviewsAsync_CollapsesNewlinesInPreview()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		await AddMessageAsync(jane.Id, me.Id, "hello\nworld", isRead: true);

		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);

		previews.ShouldHaveSingleItem().LastMessagePreview.ShouldBe("hello world");
	}

	[Fact]
	public async Task GetConversationPreviewsAsync_ExcludesNonFriendsEvenIfTheyMessaged()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var stranger = await AddUserAsync("stranger@cool.com", "stranger");
		await AddMutualFollowAsync(me.Id, jane.Id);

		await AddMessageAsync(jane.Id, me.Id, "from friend", isRead: false);
		await AddMessageAsync(stranger.Id, me.Id, "from stranger", isRead: false);

		var previews = await ServiceUnderTest.GetConversationPreviewsAsync(me.Id);

		var janePreview = previews.ShouldHaveSingleItem();
		janePreview.OtherUserId.ShouldBe(jane.Id);
		janePreview.LastMessagePreview.ShouldBe("from friend");
	}

	[Fact]
	public async Task GetConversationAsync_WhenNotFriends_ReturnsNull()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var stranger = await AddUserAsync("stranger@cool.com", "stranger");
		await AddMessageAsync(stranger.Id, me.Id, "secret", isRead: false);

		var messages = await ServiceUnderTest.GetConversationAsync(me.Id, stranger.Id, 1, 20);

		messages.ShouldBeNull();
	}

	[Fact]
	public async Task GetConversationAsync_WhenFriends_ReturnsMessages()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMessageAsync(jane.Id, me.Id, "hey", isRead: true);

		var messages = await ServiceUnderTest.GetConversationAsync(me.Id, jane.Id, 1, 20);

		messages.ShouldNotBeNull();
		messages.ShouldHaveSingleItem().Content.ShouldBe("hey");
	}

	[Fact]
	public async Task GetConversationAsync_Page1_MarksOtherUsersUnreadMessagesAsRead()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMessageAsync(jane.Id, me.Id, "unread 1", isRead: false);
		await AddMessageAsync(me.Id, jane.Id, "my msg", isRead: false);

		await ServiceUnderTest.GetConversationAsync(me.Id, jane.Id, 1, 20);

		await using var context = await DbContextFactory.CreateDbContextAsync();
		// explicit typing didn't seem to work?
		var messages = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
	.ToListAsync(context.DirectMessages);
		messages.Single(m => m.SenderId == jane.Id).IsRead.ShouldBeTrue();
		messages.Single(m => m.SenderId == me.Id).IsRead.ShouldBeFalse();
	}

	[Fact]
	public async Task GetConversationAsync_Page2_DoesNotMarkMessagesAsRead()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);
		for (var i = 0; i < 25; i++)
			await AddMessageAsync(jane.Id, me.Id, $"msg {i}", isRead: false);

		await ServiceUnderTest.GetConversationAsync(me.Id, jane.Id, 2, 20);

		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Any(m => m.IsRead).ShouldBeFalse();
	}

	[Fact]
	public async Task GetConversationAsync_ReturnsCorrectPageInChronologicalOrder()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);
		for (var i = 1; i <= 5; i++)
			await AddMessageAsync(jane.Id, me.Id, $"msg {i}");

		var page1 = await ServiceUnderTest.GetConversationAsync(me.Id, jane.Id, 1, 2);
		var page2 = await ServiceUnderTest.GetConversationAsync(me.Id, jane.Id, 2, 2);

		page1!.Select(m => m.Content).ShouldBe(["msg 4", "msg 5"]);
		page2!.Select(m => m.Content).ShouldBe(["msg 2", "msg 3"]);
	}

	[Fact]
	public async Task GetConversationAsync_SelfConversation_ReturnsNull()
	{
		var me = await AddUserAsync("me@cool.com", "me");

		var result = await ServiceUnderTest.GetConversationAsync(me.Id, me.Id, 1, 20);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task SendAsync_WhenFriends_PersistsMessageToDatabase()
	{
		var (me, jane) = await AddFriendsAsync();

		var response = await ServiceUnderTest.SendAsync(me.Id, jane.Id, "hello jane");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var saved = context.DirectMessages.Single();
        saved.Id.ShouldBe(response.Id);
        saved.SenderId.ShouldBe(me.Id);
        saved.ReceiverId.ShouldBe(jane.Id);
        saved.Content.ShouldBe("hello jane");
        saved.IsRead.ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenFriends_CreatesMessageNotificationForRecipient()
    {
        var (me, jane) = await AddFriendsAsync();

        await ServiceUnderTest.SendAsync(me.Id, jane.Id, "hello jane");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var notification = context.Notification.ShouldHaveSingleItem();
        notification.RecipientId.ShouldBe(jane.Id);
        notification.SenderId.ShouldBe(me.Id);
        notification.Type.ShouldBe(NotificationType.Message);
    }

	[Fact]
	public async Task SendAsync_WhenNotFriends_ThrowsAndDoesNotSave()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var stranger = await AddUserAsync("stranger@cool.com", "stranger");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => ServiceUnderTest.SendAsync(me.Id, stranger.Id, "hi"));

		ex.Message.ShouldBe("You can only message friends.");
		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Any().ShouldBeFalse();
	}

	[Fact]
	public async Task SendAsync_WhenFollowIsOneWay_Throws()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddFollowAsync(me.Id, jane.Id);

		await Should.ThrowAsync<InvalidOperationException>(
			() => ServiceUnderTest.SendAsync(me.Id, jane.Id, "hi"));

		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Any().ShouldBeFalse();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\n\t")]
	public async Task SendAsync_WhenContentIsBlank_ThrowsAndDoesNotSave(string content)
	{
		var (me, jane) = await AddFriendsAsync();

		await Should.ThrowAsync<ArgumentException>(
			() => ServiceUnderTest.SendAsync(me.Id, jane.Id, content));

		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Any().ShouldBeFalse();
	}

	[Fact]
	public async Task SendAsync_WhenContentExceeds256Characters_ThrowsAndDoesNotSave()
	{
		var (me, jane) = await AddFriendsAsync();

		await Should.ThrowAsync<ArgumentException>(
			() => ServiceUnderTest.SendAsync(me.Id, jane.Id, new string('a', 257)));

		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Any().ShouldBeFalse();
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_TaskNotFound_Throws()
	{
		var me = await AddUserAsync("me@cool.com", "me");

		var ex = await Should.ThrowAsync<ArgumentException>(
			() => ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, 999));

		ex.Message.ShouldBe("Task not found.");
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_OwnPublicTask_ReturnsAllFriends()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var alex = await AddUserAsync("alex@cool.com", "alex");
		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(me.Id, alex.Id);

		var task = await CreateTaskAsync(me.Id, "My task");

		var result = await ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, task.Id);

		result.Count.ShouldBe(2);
		result.ShouldContain(f => f.Id == jane.Id);
		result.ShouldContain(f => f.Id == alex.Id);
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_ExcludesFriendsAlreadySharedWith()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var alex = await AddUserAsync("alex@cool.com", "alex");
		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(me.Id, alex.Id);

		var task = await CreateTaskAsync(me.Id, "My task");
		await ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [jane.Id], task.Id);

		var result = await ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, task.Id);

		result.ShouldHaveSingleItem();
		result.Single().Id.ShouldBe(alex.Id);
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_OthersPrivateTask_Throws()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's private task", TaskVisibility.Private);

		await Should.ThrowAsync<ArgumentException>(
			() => ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, task.Id));
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_OthersFriendsOnlyTask_ReturnsMutualFriendsOnly()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var alex = await AddUserAsync("alex@cool.com", "alex"); // friend of me, not jane
		var sam = await AddUserAsync("sam@cool.com", "sam");    // friend of both

		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(me.Id, alex.Id);
		await AddMutualFollowAsync(me.Id, sam.Id);
		await AddMutualFollowAsync(jane.Id, sam.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's friends-only task", TaskVisibility.FriendsOnly);

		var result = await ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, task.Id);

		result.ShouldHaveSingleItem();
		result.Single().Id.ShouldBe(sam.Id);
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_OthersFriendsOnlyTask_CallerNotFriendOfOwner_Throws()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");

		var task = await CreateTaskAsync(jane.Id, "Jane's friends-only task", TaskVisibility.FriendsOnly);

		await Should.ThrowAsync<ArgumentException>(
			() => ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, task.Id));
	}

	[Fact]
	public async Task GetShareableFriendsForTaskAsync_OthersPublicTask_ExcludesOwnerFromResults()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's public task", TaskVisibility.Public);

		var result = await ServiceUnderTest.GetShareableFriendsForTaskAsync(me.Id, task.Id);

		result.ShouldNotContain(f => f.Id == jane.Id);
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_TaskNotFound_Throws()
	{
		var me = await AddUserAsync("me@cool.com", "me");

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [1], 999));

		ex.Message.ShouldBe("Task not found.");
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_ValidFriends_PersistsMessagesWithTaskPreview()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var alex = await AddUserAsync("alex@cool.com", "alex");
		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(me.Id, alex.Id);

		var task = await CreateTaskAsync(me.Id, "Shared task");

		var result = await ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [jane.Id, alex.Id], task.Id);

		result.Count.ShouldBe(2);
		result.ShouldAllBe(m => m.SharedTaskId == task.Id);
		result.ShouldAllBe(m => m.SharedTask != null && m.SharedTask.Title == "Shared task");
		result.ShouldContain(m => m.ReceiverId == jane.Id);
		result.ShouldContain(m => m.ReceiverId == alex.Id);

		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Count().ShouldBe(2);
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_IneligibleReceiver_IsSilentlyDropped()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var stranger = await AddUserAsync("stranger@cool.com", "stranger");
		await AddMutualFollowAsync(me.Id, jane.Id);

		var task = await CreateTaskAsync(me.Id, "Shared task");

		var result = await ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [jane.Id, stranger.Id], task.Id);

		result.ShouldHaveSingleItem();
		result.Single().ReceiverId.ShouldBe(jane.Id);

		await using var context = await DbContextFactory.CreateDbContextAsync();
		context.DirectMessages.Count().ShouldBe(1);
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_OwnerNotIncludedEvenIfRequested()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		var task = await CreateTaskAsync(me.Id, "Shared task");

		var result = await ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [jane.Id, me.Id], task.Id);

		result.ShouldHaveSingleItem();
		result.Single().ReceiverId.ShouldBe(jane.Id);
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_OthersPrivateTask_Throws()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		await AddMutualFollowAsync(me.Id, jane.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's private task", TaskVisibility.Private);

		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [jane.Id], task.Id));

		ex.Message.ShouldBe("Task not found.");
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_OthersFriendsOnlyTask_OnlyMutualFriendsOfOwnerReceiveIt()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var jane = await AddUserAsync("jane@cool.com", "jane");
		var alex = await AddUserAsync("alex@cool.com", "alex"); // friend of me, not jane
		var sam = await AddUserAsync("sam@cool.com", "sam");    // friend of both

		await AddMutualFollowAsync(me.Id, jane.Id);
		await AddMutualFollowAsync(me.Id, alex.Id);
		await AddMutualFollowAsync(me.Id, sam.Id);
		await AddMutualFollowAsync(jane.Id, sam.Id);

		var task = await CreateTaskAsync(jane.Id, "Jane's friends-only task", TaskVisibility.FriendsOnly);

		var result = await ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [alex.Id, sam.Id], task.Id);

		result.ShouldHaveSingleItem();
		result.Single().ReceiverId.ShouldBe(sam.Id);
	}

	[Fact]
	public async Task ShareTaskWithManyAsync_NoEligibleReceivers_ReturnsEmptyListAndDoesNotThrow()
	{
		var me = await AddUserAsync("me@cool.com", "me");
		var stranger = await AddUserAsync("stranger@cool.com", "stranger");

		var task = await CreateTaskAsync(me.Id, "Shared task");

		var result = await ServiceUnderTest.ShareTaskWithManyAsync(me.Id, [stranger.Id], task.Id);

		result.ShouldBeEmpty();
	}
}
