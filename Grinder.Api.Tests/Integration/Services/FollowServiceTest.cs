using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Integration.Services;

public class FollowServiceTest : BaseIntegrationTestFixture
{
    private IFollowService ServiceUnderTest => ServiceProvider.GetRequiredService<IFollowService>();

    public FollowServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    private async Task<User> AddUserToDatabaseAsync(string email, string dispalyName)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync();
        var user = new User
        {
            Email = email,
            DisplayName = dispalyName,
            PasswordHash = "not-a-real-hash",
            Country = "NZ",
            CreatedAt = FakeTimeProvider.GetUtcNow().UtcDateTime
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public async Task FollowAsync_ValidUsers_FollowRowCreatedInDatabase()
    {
        var follower = await AddUserToDatabaseAsync("follower@example.com", "follower");
        var followed = await AddUserToDatabaseAsync("followed@example.com", "followed");

        await ServiceUnderTest.FollowAsync(follower.Id, followed.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var follow = context.Follow.ShouldHaveSingleItem();
        follow.FollowerId.ShouldBe(follower.Id);
        follow.FollowedId.ShouldBe(followed.Id);
    }

    [Fact]
    public async Task FollowAsync_FollowerAndFollowedAreSame_ThrowsInvalidOperationException()
    {
        var user = await AddUserToDatabaseAsync("user@example.com", "user");

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ServiceUnderTest.FollowAsync(user.Id, user.Id));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.ShouldBeEmpty();
        context.Notification.ShouldBeEmpty();
    }

    [Fact]
    public async Task FollowAsync_AlreadyFollowing_ThrowsInvalidOperationExceptionAndDoesNotCreateDuplicateFollowOrNotification()
    {
        var follower = await AddUserToDatabaseAsync("follower@example.com", "follower");
        var followed = await AddUserToDatabaseAsync("followed@example.com", "followed");

        await ServiceUnderTest.FollowAsync(follower.Id, followed.Id);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ServiceUnderTest.FollowAsync(follower.Id, followed.Id));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.Count().ShouldBe(1);
        context.Notification.Count().ShouldBe(1);
    }

    [Fact]
    public async Task UnfollowAsync_FollowExists_FollowRowRemovedFromDatabase()
    {
        var follower = await AddUserToDatabaseAsync("follower@example.com", "follower");
        var followed = await AddUserToDatabaseAsync("followed@example.com", "followed");

        await ServiceUnderTest.FollowAsync(follower.Id, followed.Id);
        await ServiceUnderTest.UnfollowAsync(follower.Id, followed.Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnfollowAsync_FollowDoesNotExist_ThrowsInvalidOperationExceptionAndDoesNotChangeDatabase()
    {
        var follower = await AddUserToDatabaseAsync("follower@example.com", "follower");
        var followed = await AddUserToDatabaseAsync("followed@example.com", "followed");

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ServiceUnderTest.UnfollowAsync(follower.Id, followed.Id));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_FollowExists_ReturnsFollowingStatus()
    {
        var follower = await AddUserToDatabaseAsync("follower@example.com", "follower");
        var followed = await AddUserToDatabaseAsync("followed@example.com", "followed");

        await ServiceUnderTest.FollowAsync(follower.Id, followed.Id);

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            follower.Id, new List<int> { followed.Id });

        statuses[followed.Id].ShouldBe(RelationshipStatus.Following);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_FollowDoesNotExist_ReturnsFollowStatus()
    {
        var follower = await AddUserToDatabaseAsync("follower@example.com", "follower");
        var followed = await AddUserToDatabaseAsync("followed@example.com", "followed");

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            follower.Id, new List<int> { followed.Id });

        statuses[followed.Id].ShouldBe(RelationshipStatus.Follow);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_NoRelationship_ReturnsFollowStatus()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");
        var target = await AddUserToDatabaseAsync("target@example.com", "target");

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id, new List<int> { target.Id });

        statuses[target.Id].ShouldBe(RelationshipStatus.Follow);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_CurrentUserFollowsTarget_ReturnsFollowingStatus()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");
        var target = await AddUserToDatabaseAsync("target@example.com", "target");

        await ServiceUnderTest.FollowAsync(currentUser.Id, target.Id);

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id, new List<int> { target.Id });

        statuses[target.Id].ShouldBe(RelationshipStatus.Following);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_TargetFollowsCurrentUser_ReturnsFollowBackStatus()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");
        var target = await AddUserToDatabaseAsync("target@example.com", "target");

        await ServiceUnderTest.FollowAsync(target.Id, currentUser.Id);

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id, new List<int> { target.Id });

        statuses[target.Id].ShouldBe(RelationshipStatus.FollowBack);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_MutualFollow_ReturnsFriendsStatus()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");
        var target = await AddUserToDatabaseAsync("target@example.com", "target");

        await ServiceUnderTest.FollowAsync(currentUser.Id, target.Id);
        await ServiceUnderTest.FollowAsync(target.Id, currentUser.Id);

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id, new List<int> { target.Id });

        statuses[target.Id].ShouldBe(RelationshipStatus.Friends);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_MultipleTargets_ReturnsCorrectStatusForEach()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");
        var followingTarget = await AddUserToDatabaseAsync("following@example.com", "following");
        var followBackTarget = await AddUserToDatabaseAsync("followback@example.com", "followback");
        var friendTarget = await AddUserToDatabaseAsync("friend@example.com", "friend");
        var strangerTarget = await AddUserToDatabaseAsync("stranger@example.com", "stranger");

        await ServiceUnderTest.FollowAsync(currentUser.Id, followingTarget.Id);
        await ServiceUnderTest.FollowAsync(followBackTarget.Id, currentUser.Id);
        await ServiceUnderTest.FollowAsync(currentUser.Id, friendTarget.Id);
        await ServiceUnderTest.FollowAsync(friendTarget.Id, currentUser.Id);

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id,
            new List<int> { followingTarget.Id, followBackTarget.Id, friendTarget.Id, strangerTarget.Id });

        statuses[followingTarget.Id].ShouldBe(RelationshipStatus.Following);
        statuses[followBackTarget.Id].ShouldBe(RelationshipStatus.FollowBack);
        statuses[friendTarget.Id].ShouldBe(RelationshipStatus.Friends);
        statuses[strangerTarget.Id].ShouldBe(RelationshipStatus.Follow);
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_EmptyTargetList_ReturnsEmptyDictionary()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id, new List<int>());

        statuses.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRelationshipStatusesAsync_UnrelatedFollowsExist_DoesNotAffectOtherUsersStatus()
    {
        var currentUser = await AddUserToDatabaseAsync("current@example.com", "current");
        var target = await AddUserToDatabaseAsync("target@example.com", "target");
        var thirdParty = await AddUserToDatabaseAsync("third@example.com", "third");

        await ServiceUnderTest.FollowAsync(thirdParty.Id, target.Id);

        var statuses = await ServiceUnderTest.GetRelationshipStatusesAsync(
            currentUser.Id, new List<int> { target.Id });

        statuses[target.Id].ShouldBe(RelationshipStatus.Follow);
    }

    [Fact]
    public async Task GetFriendsAsync_ReturnsOnlyMutualFollows()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var friend = await AddUserToDatabaseAsync("friend@example.com", "friend");
        var oneWayFollow = await AddUserToDatabaseAsync("one-way@example.com", "one way");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.AddRange(
            new Follow { FollowerId = owner.Id, FollowedId = friend.Id },
            new Follow { FollowerId = friend.Id, FollowedId = owner.Id },
            new Follow { FollowerId = owner.Id, FollowedId = oneWayFollow.Id });
        await context.SaveChangesAsync();

        var friends = await ServiceUnderTest.GetFriendsAsync(owner.Id, owner.Id);

        friends.Count.ShouldBe(1);
        friends.Single().Id.ShouldBe(friend.Id);
        friends.Single().DisplayName.ShouldBe(friend.DisplayName);
    }

    [Fact]
    public async Task GetFriendsAsync_ReturnsFriendsInDisplayNameOrder()
    {
        var owner = await AddUserToDatabaseAsync("owner@example.com", "owner");
        var zoe = await AddUserToDatabaseAsync("zoe@example.com", "Zoe");
        var amy = await AddUserToDatabaseAsync("amy@example.com", "Amy");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Follow.AddRange(
            new Follow { FollowerId = owner.Id, FollowedId = zoe.Id },
            new Follow { FollowerId = zoe.Id, FollowedId = owner.Id },
            new Follow { FollowerId = owner.Id, FollowedId = amy.Id },
            new Follow { FollowerId = amy.Id, FollowedId = owner.Id });
        await context.SaveChangesAsync();

        var friends = await ServiceUnderTest.GetFriendsAsync(owner.Id, owner.Id);

        friends.Select(friend => friend.DisplayName)
            .ShouldBe(new[] { "Amy", "Zoe" });
    }

    [Fact]
    public async Task GetFriendCountAsync_NoFriends_ReturnsZero()
    {
        var user = await AddUserToDatabaseAsync("user@example.com", "user");

        var count = await ServiceUnderTest.GetFriendCountAsync(user.Id);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task GetFriendCountAsync_OneWayFollow_ReturnsZero()
    {
        var user = await AddUserToDatabaseAsync("user@example.com", "user");
        var other = await AddUserToDatabaseAsync("other@example.com", "other");

        await ServiceUnderTest.FollowAsync(user.Id, other.Id);

        var count = await ServiceUnderTest.GetFriendCountAsync(user.Id);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task GetFriendCountAsync_MultipleFriends_ReturnsCorrectCount()
    {
        var user = await AddUserToDatabaseAsync("user@example.com", "user");
        var friend1 = await AddUserToDatabaseAsync("friend1@example.com", "friend1");
        var friend2 = await AddUserToDatabaseAsync("friend2@example.com", "friend2");
        var oneWay = await AddUserToDatabaseAsync("oneway@example.com", "oneway");

        // Friend 1
        await ServiceUnderTest.FollowAsync(user.Id, friend1.Id);
        await ServiceUnderTest.FollowAsync(friend1.Id, user.Id);

        // Friend 2
        await ServiceUnderTest.FollowAsync(user.Id, friend2.Id);
        await ServiceUnderTest.FollowAsync(friend2.Id, user.Id);

        // One-way follow (not a friend)
        await ServiceUnderTest.FollowAsync(user.Id, oneWay.Id);

        var count = await ServiceUnderTest.GetFriendCountAsync(user.Id);

        count.ShouldBe(2);
    }
}
