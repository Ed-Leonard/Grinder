using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Grinder.Api.Tests.Integration.Services;

public class CategoryServiceTest : BaseIntegrationTestFixture
{
    private int _userId;

    private ICategoryService ServiceUnderTest =>
        ServiceProvider.GetRequiredService<ICategoryService>();

    public CategoryServiceTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await AddUser("test@test.com", "Test User");
    }

    private async Task<int> AddUser(string email, string displayName)
    {
        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            PasswordHash = string.Empty,
            Country = "NZ",
            IsVerified = true
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>
    /// GetCategoriesForUserAsync reaches categories through the user's tasks,
    /// so seeded categories have to be attached to a TaskItem to be visible.
    /// </summary>
    private async Task<IReadOnlyList<Category>> AddCategoriesViaTask(
        int count, int? userId = null, string taskTitle = "Sample Task")
    {
        var categories = Enumerable.Range(0, count)
            .Select(i => new Category { Name = $"Category {i}" })
            .ToList();

        var task = new TaskItem
        {
            Title = taskTitle,
            UserId = userId ?? _userId,
            Categories = categories
        };

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
        return categories;
    }

    [Fact]
    public async Task GetCategoriesForUser_NoCategoriesInDatabase_EmptyCollectionReturned()
    {
        var categories = await ServiceUnderTest.GetCategoriesForUserAsync(_userId);

        categories.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task GetCategoriesForUser_UserHasTaskWithCategories_AllReturned(int categoryCount)
    {
        var seeded = await AddCategoriesViaTask(categoryCount);

        var returned = await ServiceUnderTest.GetCategoriesForUserAsync(_userId);

        returned.Count().ShouldBe(categoryCount);
        returned.Select(c => c.Name)
            .ShouldBe(seeded.Select(c => c.Name), ignoreOrder: true);
    }

    [Fact]
    public async Task GetCategoriesForUser_CategoriesSpreadOverMultipleTasks_AllReturned()
    {
        await AddCategoriesViaTask(2, taskTitle: "First");

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.Add(new TaskItem
            {
                Title = "Second",
                UserId = _userId,
                Categories = [new Category { Name = "Extra" }]
            });
            await context.SaveChangesAsync();
        }

        var returned = await ServiceUnderTest.GetCategoriesForUserAsync(_userId);

        returned.Select(c => c.Name)
            .ShouldBe(["Category 0", "Category 1", "Extra"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetCategoriesForUser_SameCategoryOnTwoTasks_ReturnedOnce()
    {
        var category = new Category { Name = "Work" };

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.AddRange(
                new TaskItem { Title = "First", UserId = _userId, Categories = [category] },
                new TaskItem { Title = "Second", UserId = _userId, Categories = [category] });
            await context.SaveChangesAsync();
        }

        var returned = await ServiceUnderTest.GetCategoriesForUserAsync(_userId);

        returned.ShouldHaveSingleItem().Name.ShouldBe("Work");
    }

    [Fact]
    public async Task GetCategoriesForUser_CategoriesBelongToAnotherUser_NotReturned()
    {
        var otherUserId = await AddUser("other@test.com", "Other User");
        await AddCategoriesViaTask(3, otherUserId);

        var returned = await ServiceUnderTest.GetCategoriesForUserAsync(_userId);

        returned.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategoriesForUser_UnknownUserId_EmptyCollectionReturned()
    {
        await AddCategoriesViaTask(3);

        var returned = await ServiceUnderTest.GetCategoriesForUserAsync(_userId + 9999);

        returned.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategoriesForUser_CategoryNotAttachedToAnyTask_NotReturned()
    {
        await ServiceUnderTest.AddCategory("Orphan");

        var returned = await ServiceUnderTest.GetCategoriesForUserAsync(_userId);

        returned.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Work")]
    [InlineData("Personal")]
    [InlineData("Shopping")]
    [InlineData("University")]
    public async Task AddCategory_ValidName_CategoryAddedToDatabase(string categoryName)
    {
        var (created, wasCreated) = await ServiceUnderTest.AddCategory(categoryName);

        wasCreated.ShouldBeTrue();
        created.Name.ShouldBe(categoryName);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        var categoryInDatabase = context.Category.ShouldHaveSingleItem();
        categoryInDatabase.Id.ShouldBe(created.Id);
        categoryInDatabase.Name.ShouldBe(categoryName);
    }

    [Theory]
    [InlineData("  Work  ", "Work")]
    [InlineData("\tPersonal\n", "Personal")]
    public async Task AddCategory_SurroundingWhitespace_TrimmedNameStored(string input, string expected)
    {
        var (created, wasCreated) = await ServiceUnderTest.AddCategory(input);

        wasCreated.ShouldBeTrue();
        created.Name.ShouldBe(expected);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem().Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Work", "Work")]
    [InlineData("Work", "work")]
    [InlineData("Work", "WORK")]
    [InlineData("Work", "  work  ")]
    public async Task AddCategory_NameMatchesExistingIgnoringCase_ExistingReturnedAndNotCreated(
        string first, string second)
    {
        var (original, _) = await ServiceUnderTest.AddCategory(first);

        var (returned, wasCreated) = await ServiceUnderTest.AddCategory(second);

        wasCreated.ShouldBeFalse();
        returned.Id.ShouldBe(original.Id);
        returned.Name.ShouldBe(first); // original casing is kept

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task AddCategory_NameExistsOnAnotherUsersTask_ExistingCategoryReused()
    {
        var otherUserId = await AddUser("other@test.com", "Other User");
        var seeded = await AddCategoriesViaTask(1, otherUserId); // "Category 0"

        var (returned, wasCreated) = await ServiceUnderTest.AddCategory("Category 0");

        wasCreated.ShouldBeFalse();
        returned.Id.ShouldBe(seeded[0].Id);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n  \t ")]
    public async Task AddCategory_BlankName_ThrowsArgumentException(string categoryName)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => ServiceUnderTest.AddCategory(categoryName));

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddCategory_NullName_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => ServiceUnderTest.AddCategory(null!));
    }
}
