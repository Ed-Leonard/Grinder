using Grinder.Api.Models.Entities;

namespace Grinder.Api.Models.DTOs;

public class LiveStreamerResponse
{
	public int Id { get; set; }
	public string DisplayName { get; set; } = string.Empty;
}

public class LiveStreamSummaryResponse
{
	public int Id { get; set; }
	public string RoomName { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string TaskTitle { get; set; } = string.Empty;
	public int TaskId { get; set; }
	public LiveStreamerResponse Streamer { get; set; } = null!;
	public int ViewerCount { get; set; }
	public DateTimeOffset StartedAt { get; set; }
	public TaskVisibility Visibility { get; set; }
}

public class LiveStreamPageResponse
{
	public List<LiveStreamSummaryResponse> Items { get; set; } = [];
	public bool HasMore { get; set; }
}
