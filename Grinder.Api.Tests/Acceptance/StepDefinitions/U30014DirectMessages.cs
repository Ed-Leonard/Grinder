using System.Net;
using System.Net.Http.Json;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Tests.Acceptance.Setup;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using Shouldly;

namespace Grinder.Api.Tests.Acceptance.StepDefinitions;

[Binding]
public class U30014DirectMessagesSteps
{
    private readonly AcceptanceTestFixture _fixture;
    private int _otherUserId;
    private string _otherUserDisplayName = "";
    private string _draftMessage = "";
    private DirectMessageResponse? _sentMessage;
    private const string MessageContent = "Hey, are you free this weekend?";

    public U30014DirectMessagesSteps(AcceptanceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Given("I am friends with someone")]
    public async Task GivenIAmFriendsWithSomeone()
    {
        var otherUser = new User
        {
            Email = "friend@example.com",
            DisplayName = "Alex",
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            IsVerified = true
        };

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.Users.Add(otherUser);
        await context.SaveChangesAsync();

        var currentUserId = _fixture.CurrentUserId!.Value;
        context.Follow.AddRange(
            new Follow { FollowerId = currentUserId, FollowedId = otherUser.Id },
            new Follow { FollowerId = otherUser.Id, FollowedId = currentUserId });
        await context.SaveChangesAsync();

        _otherUserId = otherUser.Id;
        _otherUserDisplayName = otherUser.DisplayName;
    }

    [Given("I am viewing a conversation")]
    public async Task GivenIAmViewingAConversation()
    {
        var response = await _fixture.HttpClient.GetAsync(
            $"api/directmessages/conversation/{_otherUserId}?page=1&limit=20");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [When("I type a valid message between 1 and 256 characters")]
    public void WhenITypeAValidMessage()
    {
        _draftMessage = "Hey! Want to catch up later today?";
    }

    [When("I click on the 'Send' button")]
    public async Task WhenIClickOnTheSendButton()
    {
        var messageService = _fixture.ServiceProvider.GetRequiredService<IDirectMessageService>();
        _sentMessage = await messageService.SendAsync(_fixture.CurrentUserId!.Value, _otherUserId, _draftMessage);
    }

    [Then("the message is sent to my friend")]
    public void ThenTheMessageIsSentToMyFriend()
    {
        _sentMessage.ShouldNotBeNull();
        _sentMessage!.SenderId.ShouldBe(_fixture.CurrentUserId!.Value);
        _sentMessage.ReceiverId.ShouldBe(_otherUserId);
        _sentMessage.Content.ShouldBe(_draftMessage);
    }

    [Then("persisted in the chat history")]
    public async Task ThenPersistedInTheChatHistory()
    {
        var response = await _fixture.HttpClient.GetAsync(
            $"api/directmessages/conversation/{_otherUserId}?page=1&limit=20");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var conversation = await response.Content.ReadFromJsonAsync<ConversationPageResponse>();
        conversation.ShouldNotBeNull();
        conversation!.Messages.ShouldContain(m => m.Content == _draftMessage);
    }

    [Then("shows up in the conversation as being sent")]
    public async Task ThenShowsUpInTheConversationAsBeingSent()
    {
        var response = await _fixture.HttpClient.GetAsync(
            $"api/directmessages/conversation/{_otherUserId}?page=1&limit=20");
        var conversation = await response.Content.ReadFromJsonAsync<ConversationPageResponse>();

        var message = conversation!.Messages.Single(m => m.Content == _draftMessage);
        message.SenderId.ShouldBe(_fixture.CurrentUserId!.Value);
    }

    [When("they send me a direct message")]
    public async Task WhenTheySendMeADirectMessage()
    {
        var messageService = _fixture.ServiceProvider.GetRequiredService<IDirectMessageService>();
        await messageService.SendAsync(_otherUserId, _fixture.CurrentUserId!.Value, MessageContent);
    }

    [Then("I am notified that I have received a message via the in-app notification bell indicator and notification page")]
    public async Task ThenIAmNotifiedViaTheBellAndNotificationPage()
    {
        var unreadResponse = await _fixture.HttpClient.GetAsync("api/notifications/unread-count");
        unreadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var unread = await unreadResponse.Content.ReadFromJsonAsync<UnreadCountResponse>();
        unread.ShouldNotBeNull();
        unread!.Count.ShouldBeGreaterThanOrEqualTo(1);

        var listResponse = await _fixture.HttpClient.GetAsync("api/notifications");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await listResponse.Content.ReadFromJsonAsync<NotificationPageDto>();
        page.ShouldNotBeNull();

        var notification = page!.Items.ShouldHaveSingleItem();
        notification.Type.ShouldBe(NotificationType.Message);
        notification.SenderId.ShouldBe(_otherUserId);
        notification.SenderName.ShouldBe(_otherUserDisplayName);
        notification.Read.ShouldBeFalse();
    }

    [Then("there is an unread indicator on the Messages page")]
    public async Task ThenThereIsAnUnreadIndicatorOnTheMessagesPage()
    {
        var response = await _fixture.HttpClient.GetAsync("api/directmessages/conversations");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var previews = await response.Content.ReadFromJsonAsync<List<ConversationPreviewResponse>>();

        var conversation = previews.ShouldHaveSingleItem();
        conversation.OtherUserId.ShouldBe(_otherUserId);
        conversation.HasUnread.ShouldBeTrue();
        conversation.UnreadCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [When("I open the direct message")]
    public async Task WhenIOpenTheDirectMessage()
    {
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync(
            $"api/directmessages/conversation/{_otherUserId}?page=1&limit=20");
    }

    [Then("it shows up in the conversation without reloading the page")]
    public async Task ThenItShowsUpInTheConversation()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);

        var conversation = await _fixture.LastResponse.Content.ReadFromJsonAsync<ConversationPageResponse>();
        conversation.ShouldNotBeNull();
        conversation!.Messages.ShouldContain(m => m.Content == MessageContent && m.SenderId == _otherUserId);
    }

    [Then("the unread indicator is cleared")]
    public async Task ThenTheUnreadIndicatorIsCleared()
    {
        var response = await _fixture.HttpClient.GetAsync("api/directmessages/conversations");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var previews = await response.Content.ReadFromJsonAsync<List<ConversationPreviewResponse>>();

        var conversation = previews.ShouldHaveSingleItem();
        conversation.HasUnread.ShouldBeFalse();
        conversation.UnreadCount.ShouldBe(0);
    }

    [Given("I have opened an unread conversation")]
    public async Task GivenIHaveOpenedAnUnreadConversation()
    {
        var messageService = _fixture.ServiceProvider.GetRequiredService<IDirectMessageService>();
        await messageService.SendAsync(_otherUserId, _fixture.CurrentUserId!.Value, MessageContent);

        var response = await _fixture.HttpClient.GetAsync(
            $"api/directmessages/conversation/{_otherUserId}?page=1&limit=20");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [When("I navigate back to the Direct Messages page")]
    public async Task WhenINavigateBackToTheDirectMessagesPage()
    {
        _fixture.LastResponse = await _fixture.HttpClient.GetAsync("api/directmessages/conversations");
    }

    [Then("that conversation no longer shows an unread indicator")]
    public async Task ThenThatConversationNoLongerShowsAnUnreadIndicator()
    {
        _fixture.LastResponse.ShouldNotBeNull();
        _fixture.LastResponse!.StatusCode.ShouldBe(HttpStatusCode.OK);

        var previews = await _fixture.LastResponse.Content.ReadFromJsonAsync<List<ConversationPreviewResponse>>();
        var conversation = previews.ShouldHaveSingleItem();
        conversation.HasUnread.ShouldBeFalse();
    }

    private record UnreadCountResponse(int Count);
}
