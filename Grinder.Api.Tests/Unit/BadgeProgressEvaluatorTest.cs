using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Shouldly;

namespace Grinder.Api.Tests.Unit;

public class BadgeProgressEvaluatorTest
{
    private static readonly DateTimeOffset TestNow = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static BadgeProgressEvaluator CreateEvaluator()
    {
        return new BadgeProgressEvaluator(new FakeTimeProvider(TestNow), Substitute.For<IFollowService>());
    }

    [Theory]
    [InlineData(BadgeCriteriaKeys.TasksDone, 1)]
    [InlineData(BadgeCriteriaKeys.TasksCreated, 2)]
    [InlineData(BadgeCriteriaKeys.TasksOverdue, 3)]
    [InlineData(BadgeCriteriaKeys.ClipsCreated, 4)]
    [InlineData(BadgeCriteriaKeys.SubtasksCreated, 5)]
    [InlineData(BadgeCriteriaKeys.TasksCategorized, 6)]
    [InlineData(BadgeCriteriaKeys.FriendsMade, 7)]
    [InlineData("SomeUnknownCriteriaKey", 0)]
    public void GetCurrentValue_ReturnsTheStatMatchingCriteriaKey(string criteriaKey, int expected)
    {
        var evaluator = CreateEvaluator();
        var badge = new Badge { CriteriaKey = criteriaKey, Threshold = 100 };
        var stats = new BadgeCriteriaStats(
            TasksDoneCount: 1,
            TasksCreatedCount: 2,
            TasksOverdueCount: 3,
            ClipsCreatedCount: 4,
            SubtasksCreatedCount: 5,
            TasksCategorizedCount: 6,
            FriendsCount: 7);

        evaluator.GetCurrentValue(badge, stats).ShouldBe(expected);
    }

    [Theory]
    [InlineData(4, 5, false)] // below threshold
    [InlineData(5, 5, true)]  // exactly at threshold
    [InlineData(6, 5, true)]  // above threshold
    public void IsCriteriaMet_ComparesCurrentValueAgainstThreshold(int currentValue, int threshold, bool expected)
    {
        var evaluator = CreateEvaluator();
        var badge = new Badge { CriteriaKey = BadgeCriteriaKeys.TasksDone, Threshold = threshold };
        var stats = new BadgeCriteriaStats(currentValue, 0, 0, 0, 0, 0, 0);

        evaluator.IsCriteriaMet(badge, stats).ShouldBe(expected);
    }

    [Fact]
    public void IsCriteriaMet_UnknownCriteriaKey_IsNeverMet()
    {
        var evaluator = CreateEvaluator();
        var badge = new Badge { CriteriaKey = "SomeUnknownCriteriaKey", Threshold = 1 };
        var stats = new BadgeCriteriaStats(100, 100, 100, 100, 100, 100, 100);

        evaluator.IsCriteriaMet(badge, stats).ShouldBeFalse();
    }
}
