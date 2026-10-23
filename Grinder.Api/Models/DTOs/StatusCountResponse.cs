namespace Grinder.Api.Models.DTOs;

public class StatusCountResponse
{
    public int Todo { get; init; }
    public int InProgress { get; init; }
    public int Done { get; init; }
    public int Overdue { get; init; }
}
