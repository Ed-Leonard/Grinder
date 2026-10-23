namespace Grinder.Api.Models.Attachments;

/// <summary>A file on disk, ready to be streamed.</summary>
public sealed record StoredFile(string PhysicalPath, string ContentType, string Version);
