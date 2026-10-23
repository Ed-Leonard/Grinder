namespace Grinder.Api.Models.DTOs;

public record WrappedEligibilityResponse(bool IsEligible, int? MostRecentEligibleYear, int? CurrentEligibleYear, bool Dismissed);

public record DismissWrappedRequest(int Year);

public record WrappedMostReactedToResponse(int Id, string Title, int ReactionCount);

public record WrappedMostRepliedToResponse(int Id, int TaskId, string Content, int ReplyCount);

public record WrappedCategoryStatResponse(string Name, int TaskCount);

public record WrappedExtraStats(
    int BadgesEarnedCount,
    int NumberOfFriendsMade,
    int NumberOfNewFollowers,
    int NumberOfMessagesSent,
    int NumberOfSharedTasks,
    int NumberOfCommentsWritten,
    int NumberOfReactionsReceived,
    int NumberOfTasksOverdue,
    WrappedProductiveMonthResponse? MostProductiveMonth
);

public record WrappedProductiveMonthResponse(string Month, int TaskCount);

public record WrappedStatsResponse(
    int TasksStarted,
    int TasksCompleted,
    int CompletionPercent,
    WrappedMostReactedToResponse? MostLikedTask,
    WrappedMostRepliedToResponse? MostLikedComment,
    IReadOnlyList<WrappedCategoryStatResponse>? TopCategories,
    WrappedExtraStats? ExtraStats
);

public record WrappedConstantEligibilityResponse(
    bool CurrentYearEligible, int CurrentYear,
    bool PreviousYearEligible, int PreviousYear);
