using Microsoft.Extensions.Time.Testing;
using Grinder.Api.Services;
using Xunit;

namespace Grinder.Api.Tests.Unit;

public class CreateTaskServiceTest
{
    private static readonly DateTimeOffset TestNow =
        new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, null, null, null)] // no start or due date/time
    [InlineData(-1, null, 1, null)] // past start date, future due date
    [InlineData(0, null, 0, null)] // today, no time
    [InlineData(0, 1, 0, 2)] // today + future time
    [InlineData(1, null, 1, null)] // future date, no time
    public void ValidateStartDateTime_ValidCases(int? startDateOffset, int? startTimeOffset, int? dueDateOffset, int? dueTimeOffset)
    {
        var clock = new FakeTimeProvider(TestNow);
        var nowLocal = clock.GetLocalNow().DateTime;
        DateOnly? startDate = startDateOffset is null ? null : DateOnly.FromDateTime(nowLocal.AddDays(startDateOffset.Value));
        TimeOnly? startTime = startTimeOffset is null ? null : TimeOnly.FromDateTime(nowLocal.AddHours(startTimeOffset.Value));
        DateOnly? dueDate = dueDateOffset is null ? null : DateOnly.FromDateTime(nowLocal.AddDays(dueDateOffset.Value));
        TimeOnly? dueTime = dueTimeOffset is null ? null : TimeOnly.FromDateTime(nowLocal.AddHours(dueTimeOffset.Value));

        var exception = Record.Exception(() =>
            TaskService.ValidateStartDateTime(startDate, startTime, dueDate, dueTime, clock));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null, 0, null, null, "startTime")] // time, no date
    [InlineData(2, null, 1, null, "startDate")] // future start date after due date
    [InlineData(1, 2, 1, 1, "startDate")] // equal date but start time after due time
    public void ValidateStartDateTime_InvalidCases(int? startDateOffset, int? startTimeOffset, int? dueDateOffset, int? dueTimeOffset, string expectedParamName)
    {
        var clock = new FakeTimeProvider(TestNow);
        var nowLocal = clock.GetLocalNow().DateTime;
        DateOnly? startDate = startDateOffset is null ? null : DateOnly.FromDateTime(nowLocal.AddDays(startDateOffset.Value));
        TimeOnly? startTime = startTimeOffset is null ? null : TimeOnly.FromDateTime(nowLocal.AddHours(startTimeOffset.Value));
        DateOnly? dueDate = dueDateOffset is null ? null : DateOnly.FromDateTime(nowLocal.AddDays(dueDateOffset.Value));
        TimeOnly? dueTime = dueTimeOffset is null ? null : TimeOnly.FromDateTime(nowLocal.AddHours(dueTimeOffset.Value));

        var exception = Assert.Throws<ArgumentException>(() =>
            TaskService.ValidateStartDateTime(startDate, startTime, dueDate, dueTime, clock));
        Assert.Equal(expectedParamName, exception.ParamName);
    }


    [Theory]
    [InlineData(2, null)] // future date
    [InlineData(0, null)] // today, no time
    [InlineData(0, 1)] // today + future time
    public void ValidateDueDateTime_ValidCases(int? dayOffset, int? hourOffset)
    {
        var clock = new FakeTimeProvider(TestNow);
        var nowLocal = clock.GetLocalNow().DateTime;
        DateOnly? d = dayOffset is null ? null : DateOnly.FromDateTime(nowLocal.AddDays(dayOffset.Value));
        TimeOnly? t = hourOffset is null ? null : TimeOnly.FromDateTime(nowLocal.AddHours(hourOffset.Value));

        var exception = Record.Exception(() =>
            TaskService.ValidateDueDateTime(d, t, clock));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null, 0, "dueDate")] // time, no date, date required
    [InlineData(-1, null, "dueDate")] // past date
    [InlineData(0, -1, "dueDate")] // today + past time
    [InlineData(-1, 5, "dueDate")] // past date + future time
    public void ValidateDueDateTime_InvalidCases(int? dayOffset, int? hourOffset, string expectedParamName)
    {
        var clock = new FakeTimeProvider(TestNow);
        var nowLocal = clock.GetLocalNow().DateTime;
        DateOnly? d = dayOffset is null ? null : DateOnly.FromDateTime(nowLocal.AddDays(dayOffset.Value));
        TimeOnly? t = hourOffset is null ? null : TimeOnly.FromDateTime(nowLocal.AddHours(hourOffset.Value));

        var exception = Assert.Throws<ArgumentException>(() =>
            TaskService.ValidateDueDateTime(d, t, clock));
        Assert.Equal(expectedParamName, exception.ParamName);
    }




}
