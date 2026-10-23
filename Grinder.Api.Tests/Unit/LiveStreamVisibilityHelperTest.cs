using Shouldly;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Utilities;

namespace Grinder.Api.Tests.Unit;

public class LiveStreamVisibilityHelperTest
{
    [Theory]
    [InlineData(TaskVisibility.FriendsOnly, RelationshipStatus.Friends, true)]
    [InlineData(TaskVisibility.FriendsOnly, RelationshipStatus.Following, false)]
    [InlineData(TaskVisibility.FriendsOnly, RelationshipStatus.FollowBack, false)]
    [InlineData(TaskVisibility.FriendsOnly, RelationshipStatus.Follow, false)]
    [InlineData(TaskVisibility.Public, RelationshipStatus.Follow, true)]
    [InlineData(TaskVisibility.Public, RelationshipStatus.Friends, true)]
    public void IsVisibleToViewer_VariousRelationships_ReturnsExpected(
        TaskVisibility visibility, RelationshipStatus relationship, bool expected)
    {
        LiveStreamVisibilityHelper.IsVisibleToViewer(visibility, relationship).ShouldBe(expected);
    }

    [Theory]
    [InlineData(RelationshipStatus.Friends, 0)]
    [InlineData(RelationshipStatus.Following, 1)]
    [InlineData(RelationshipStatus.Follow, 2)]
    [InlineData(RelationshipStatus.FollowBack, 2)]
    public void GetRank_VariousRelationships_ReturnsExpected(RelationshipStatus relationship, int expected)
    {
        LiveStreamVisibilityHelper.GetRank(relationship).ShouldBe(expected);
    }
}