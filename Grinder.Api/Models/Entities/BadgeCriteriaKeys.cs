namespace Grinder.Api.Models.Entities;

/// <summary>
/// Well-known values for <see cref="Badge.CriteriaKey"/>. Every key here must be handled by
/// BadgeProgressEvaluator, and every key BadgeProgressEvaluator handles must be backed by data
/// that's actually tracked in the current schema.
/// </summary>
public static class BadgeCriteriaKeys
{
    public const string TasksDone = "TasksDone";
    public const string TasksCreated = "TasksCreated";
    public const string TasksOverdue = "TasksOverdue";
    public const string ClipsCreated = "ClipsCreated";
    public const string SubtasksCreated = "SubtasksCreated";
    public const string TasksCategorized = "TasksCategorized";
    public const string FriendsMade = "FriendsMade";
}
