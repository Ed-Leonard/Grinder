namespace Grinder.Api.Models.DTOs;

public record ProfileResponse(
    int Id,
    string Email,
    string DisplayName,
    string Country
);
