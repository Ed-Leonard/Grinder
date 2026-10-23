using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Services;

/// <summary>
/// Provides methods for retrieving and managing categories.
/// </summary>
public interface ICategoryService
{
    Task<IEnumerable<Category>> GetCategoriesForUserAsync(int userId);
    Task<(Category Category, bool WasCreated)> AddCategory(string categoryName);
}

/// <summary>
/// Service responsible for performing category-related database operations.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    /// <summary>
    /// Creates a new instance of the <see cref="CategoryService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">
    /// Factory used to create instances of <see cref="DatabaseContext"/>.
    /// </param>
    public CategoryService(IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Retrieves all categories stored in the database.
    /// </summary>
    /// <returns>
    /// A collection containing all categories.
    /// </returns>
    public async Task<IEnumerable<Category>> GetCategoriesForUserAsync(int userId)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync();

        return await context.TaskItems
            .Where(t => t.UserId == userId)
            .SelectMany(t => t.Categories)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new category and saves it to the database.
    /// </summary>
    /// <param name="categoryName">
    /// The name of the category to create.
    /// </param>
    /// <returns>
    /// The newly created <see cref="Category"/> if the category was created
    /// successfully; otherwise <c>null</c> if <paramref name="categoryName"/> is
    /// <c>null</c>.
    /// </returns>
    public async Task<(Category Category, bool WasCreated)> AddCategory(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new ArgumentException("Category name cannot be empty.", nameof(categoryName));
        }

        var normalized = categoryName.Trim();

        await using var context = await _dbContextFactory.CreateDbContextAsync();

        var existing = await context.Category
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalized.ToLower());

        if (existing is not null)
        {
            return (existing, false);
        }

        var category = new Category { Name = normalized };
        context.Category.Add(category);

        await context.SaveChangesAsync();

        return (category, true);
    }
}
