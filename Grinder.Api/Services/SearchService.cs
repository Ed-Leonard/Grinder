using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;

namespace Grinder.Api.Services;

/// <summary>
/// Defines the contract for the search service, which is responsible for retrieving 
/// search results based on user queries.
/// </summary>
public interface ISearchService
{
    Task<SearchResponseDto> GetResultsAsync(int userId, string query, int page = 1, int limit = 20);
}

/// <summary>
/// Implements the search service, providing functionality to retrieve search results 
/// based on user queries.
/// </summary>
public class SearchService : ISearchService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IFollowService _followService;

    public SearchService(IDbContextFactory<DatabaseContext> dbContextFactory, IFollowService followService)
    {
        _dbContextFactory = dbContextFactory;
        _followService = followService;
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// Credit to https://en.wikipedia.org/wiki/Levenshtein_distance and Claude for helping with this implementation.
    /// </summary>
    /// <param name="a">The first string.</param>
    /// <param name="b">The second string.</param>
    /// <returns>The Levenshtein distance between the two strings.</returns>
    internal static int LevenshteinDistance(string a, string b)
    {
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();

        var dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }

        return dp[a.Length, b.Length];
    }

    /// <summary>
    /// Determines whether any word in the given text approximately matches the query,
    /// either as a substring match or within a Levenshtein distance threshold that scales
    /// with the query's length.
    /// </summary>
    /// <param name="text">The text to search, split into words on whitespace and punctuation.</param>
    /// <param name="query">The search query to match against each word.</param>
    /// <returns>
    /// <c>true</c> if any word in <paramref name="text"/> contains <paramref name="query"/>
    /// as a substring, or is within the allowed edit-distance threshold of it; otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsFuzzyMatch(string text, string query)
    {
        var words = text
            .ToLowerInvariant()
            .Split(
                new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_', '/' },
                StringSplitOptions.RemoveEmptyEntries);

        var queryWords = query
        .ToLowerInvariant()
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(w => w.Length > 1)
        .ToArray();

        if (queryWords.Length == 0)
            return false;

        return queryWords.All(qw =>
        {
            var threshold = Math.Min(2, qw.Length / 2);
            return words.Any(word =>
                word.Contains(qw) ||
                LevenshteinDistance(word, qw) <= threshold);
        });
    }

    /// <summary>
    /// Scores how well a query matches a piece of text — lower is stronger.
    /// Each query word contributes its best-case edit distance against any word
    /// in the text (0 if it's a direct substring match).
    /// </summary>
    internal static int MatchScore(string text, string query)
    {
        var words = text
            .ToLowerInvariant()
            .Split(
                new[] { ' ', ',', '.', '!', '?', ':', ';', '-', '_', '/' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .ToArray();

        var queryWords = query
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .ToArray();

        if (queryWords.Length == 0 || words.Length == 0)
            return int.MaxValue;

        return queryWords.Sum(qw =>
            words.Min(word => word.Contains(qw) ? 0 : LevenshteinDistance(word, qw)));
    }

    /// <summary>
    /// Determines whether a task is visible to the searching user: their own tasks,
    /// Public tasks from anyone, or FriendsOnly tasks from users they follow.
    /// </summary>
    private static bool CanView(TaskItem task, IReadOnlyDictionary<int, RelationshipStatus> ownerStatuses)
    {
        return task.Visibility switch
        {
            TaskVisibility.Public => true,
            TaskVisibility.FriendsOnly => ownerStatuses.TryGetValue(task.UserId, out var status)
                && status == RelationshipStatus.Friends,
            TaskVisibility.Private => false,
            _ => false
        };
    }

    ///<summary>
    /// Retrieves search results based on the provided criteria.
    /// </summary>
    /// <param name="userId">The ID of the user performing the search.</param>
    public async Task<SearchResponseDto> GetResultsAsync(int userId, string query, int page = 1, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchResponseDto
            {
                Users = [],
                Tasks = [],
                Page = page,
                TotalPages = 0
            };
        }

        using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var lowerQuery = query.ToLowerInvariant();

        // --- Users ---
        var prefix = lowerQuery.Substring(0, Math.Min(1, query.Length));

        var candidates = await dbContext.Users
            .Where(u =>
                u.Id != userId &&
                (u.DisplayName.ToLower().Contains(lowerQuery) ||
                u.DisplayName.ToLower().StartsWith(prefix)))
            .ToListAsync();

        var threshold = Math.Max(1, lowerQuery.Length / 2);

        var userResults = candidates
            .Select(u =>
            {
                var name = u.DisplayName.ToLowerInvariant();
                var isSubstring = name.Contains(lowerQuery);
                var distance = isSubstring ? 0 : LevenshteinDistance(name, lowerQuery);
                return new { u, isSubstring, distance };
            })
            .Where(x => x.isSubstring || x.distance <= threshold)
            .OrderBy(x => x.distance)
            .Select(x => x.u)
            .ToList();

        // Compute relationship statuses for all matched users in one batch
        var statuses = await _followService.GetRelationshipStatusesAsync(
            userId, userResults.Select(u => u.Id));

        // --- Tasks ---
        var queryWords = lowerQuery
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 1)
            .ToArray();

        var prefixes = queryWords.Select(w => w.Substring(0, Math.Min(3, w.Length))).ToArray();

        var taskCandidates = await dbContext.TaskItems
            .Include(t => t.Categories)
            .Include(t => t.TaskAttachments)
            .Where(t =>
                t.UserId != userId &&
                (prefixes.Any(p => t.Title.ToLower().Contains(p)) ||
                (t.Description != null && prefixes.Any(p => t.Description.ToLower().Contains(p))) ||
                t.Categories.Any(c => prefixes.Any(p => c.Name.ToLower().Contains(p)))))
            .ToListAsync();

        var taskOwnerIds = taskCandidates.Select(t => t.UserId).Distinct();
        var taskOwnerStatuses = await _followService.GetRelationshipStatusesAsync(userId, taskOwnerIds);

        var candidateTaskIds = taskCandidates.Select(t => t.Id).ToList();

        var reactionCounts = await dbContext.TaskReactions
            .Where(r => candidateTaskIds.Contains(r.TaskItemId))
            .GroupBy(r => r.TaskItemId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count);

        var commentCounts = await dbContext.Comments
            .Where(c => !c.IsDeleted && candidateTaskIds.Contains(c.TaskId))
            .GroupBy(c => c.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count);

        int InteractionCount(int taskId) =>
            (reactionCounts.TryGetValue(taskId, out var r) ? r : 0) +
            (commentCounts.TryGetValue(taskId, out var c) ? c : 0);
        var taskResults = taskCandidates
        .Where(t => CanView(t, taskOwnerStatuses))
        .Select(t =>
        {
            var matchesTitle = IsFuzzyMatch(t.Title, lowerQuery);
            var matchesCategory = t.Categories.Any(c => IsFuzzyMatch(c.Name, lowerQuery));
            var matchesDescription = IsFuzzyMatch(t.Description ?? "", lowerQuery);

            int tier =
                matchesTitle ? 0 :
                matchesDescription ? 1 :
                matchesCategory ? 2 :
                3;

            int score = tier switch
            {
                0 => MatchScore(t.Title, lowerQuery),
                1 => MatchScore(t.Description ?? "", lowerQuery),
                2 => t.Categories
                        .Select(c => MatchScore(c.Name, lowerQuery))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min(),
                _ => int.MaxValue
            };

            return new { Task = t, Tier = tier, Score = score, Interactions = InteractionCount(t.Id) };
        })
        .Where(x => x.Tier < 3)
        .OrderBy(x => x.Tier)
        .ThenBy(x => x.Score)
        .ThenByDescending(x => x.Interactions)
        .ThenBy(x => x.Task.Id) // stable tiebreaker so ordering is deterministic across pages
        .Select(x => x.Task)
        .ToList();

        if (page < 1) page = 1;
        if (limit < 1) limit = 20;

        // Users and tasks currently share a single page cursor, so TotalPages
        // must reflect whichever result set has more pages — using only one
        // type's count here would cause pagination to stop early for the
        // other type (e.g. reporting 1 total page when there's a second page
        // of matching users but zero matching tasks).
        var taskTotalPages = (int)Math.Ceiling(taskResults.Count / (double)limit);
        var userTotalPages = (int)Math.Ceiling(userResults.Count / (double)limit);
        var totalPages = Math.Max(taskTotalPages, userTotalPages);

        var pagedTasks = taskResults
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        var pagedUsers = userResults
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToList();

        // --- Assemble Results ---
        var searchResults = new SearchResponseDto
        {
            Users = pagedUsers
                .Select(u => UserSearchResultDto.FromEntity(u, statuses[u.Id]))
                .ToList(),
            Tasks = pagedTasks
                .Select(t => TaskResponse.FromEntity(t, userId))
                .ToList(),
            Page = page,
            TotalPages = totalPages
        };

        return searchResults;
    }
}