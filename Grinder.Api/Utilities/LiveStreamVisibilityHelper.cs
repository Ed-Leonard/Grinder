using Grinder.Api.Models.Entities;
using Grinder.Api.Services;

namespace Grinder.Api.Utilities;

/// <summary>
/// Pure decision logic for whether a viewer can see a livestream, and how it should
/// be ranked relative to others, based on the viewer's relationship to the streamer.
/// </summary>
public static class LiveStreamVisibilityHelper
{
    /// <summary>
    /// Determines if a viewer can see a livestream based on its visibility and the viewer's relationship to the streamer.
    /// Friends Only streams are only visible to the streamer's friends; Public streams are visible to anyone.
    /// </summary>
    /// <param name="visibility"> The visibility of the stream.</param>
    /// <param name="relationship"> The relationship of the viewer to the streamer.</param>
    /// <returns> True if the viewer can see the stream, false otherwise.</returns>
    public static bool IsVisibleToViewer(TaskVisibility visibility, RelationshipStatus relationship)
    {
        return visibility switch
        {
            TaskVisibility.Public => true,
            TaskVisibility.FriendsOnly => relationship == RelationshipStatus.Friends,
            _ => false
        };
    }

    /// <summary>
    /// Determines a rank for sorting streams based on the viewer's relationship to the streamer.
    /// Lower rank sorts first: friends' streams, then followed public streams, then everyone else.
    /// </summary>
    /// <param name="relationship"> The relationship of the viewer to the streamer.</param>
    /// <returns> An integer rank for sorting streams.</returns>
    public static int GetRank(RelationshipStatus relationship)
    {
        return relationship switch
        {
            RelationshipStatus.Friends => 0,
            RelationshipStatus.Following => 1,
            _ => 2
        };
    }
}
