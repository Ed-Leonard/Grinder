using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Models.Entities;

[Index(nameof(Name), IsUnique = true)]
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
