namespace Grinder.Api.Models.DTOs;

public record ReorderTasksRequest(
    int Id,
    int? ParentId,
    int Order
);
