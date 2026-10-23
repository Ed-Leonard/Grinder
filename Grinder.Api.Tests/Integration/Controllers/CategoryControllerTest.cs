using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Models.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Grinder.Api.Tests.Integration.Controllers;

public class CategoryControllerTest : BaseIntegrationTestFixture
{
    private int _userId;

    public CategoryControllerTest(WebApplicationFactory<Program> webAppFactory)
        : base(webAppFactory, handleCookies: true) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await RegisterAndLogin();
    }

    private async Task RegisterAndLogin()
    {
        var user = CreateUser("test@test.com", "Test User");
        user.PasswordHash = ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, "Password123!");

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        _userId = user.Id;

        var login = await HttpClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("test@test.com", "Password123!"));

        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static User CreateUser(string email, string displayName) => new()
    {
        Email = email,
        DisplayName = displayName,
        PasswordHash = string.Empty,
        Country = "NZ",
        IsVerified = true
    };

    private async Task<int> AddOtherUser()
    {
        var user = CreateUser("other@test.com", "Other User");
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>
    /// Categories are only visible through a task that owns them, so seeding
    /// has to go via a TaskItem rather than inserting Category rows directly.
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
    public async Task GetCategories_NoCategoriesExist_EmptyCollectionReturned()
    {
        var response = await HttpClient.GetAsync("/api/Category");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = await response.Content.ReadFromJsonAsync<IEnumerable<Category>>();
        returned.ShouldNotBeNull();
        returned.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task GetCategories_UserHasTaskWithCategories_AllReturned(int count)
    {
        var seeded = await AddCategoriesViaTask(count);

        var response = await HttpClient.GetAsync("/api/Category");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = await response.Content.ReadFromJsonAsync<ICollection<Category>>();
        returned.ShouldNotBeNull();
        returned.Count.ShouldBe(count);
        returned.Select(c => c.Name)
            .ShouldBe(seeded.Select(c => c.Name), ignoreOrder: true);
    }

    [Fact]
    public async Task GetCategories_CategoriesBelongToAnotherUsersTask_NotReturned()
    {
        var otherUserId = await AddOtherUser();
        await AddCategoriesViaTask(3, otherUserId);

        var response = await HttpClient.GetAsync("/api/Category");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = await response.Content.ReadFromJsonAsync<ICollection<Category>>();
        returned.ShouldNotBeNull();
        returned.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategories_CategoryOnMultipleTasks_ReturnedOnce()
    {
        var category = new Category { Name = "Work" };

        await using (var context = await DbContextFactory.CreateDbContextAsync())
        {
            context.TaskItems.AddRange(
                new TaskItem { Title = "First", UserId = _userId, Categories = [category] },
                new TaskItem { Title = "Second", UserId = _userId, Categories = [category] });
            await context.SaveChangesAsync();
        }

        var response = await HttpClient.GetAsync("/api/Category");

        var returned = await response.Content.ReadFromJsonAsync<ICollection<Category>>();
        returned.ShouldNotBeNull();
        returned.ShouldHaveSingleItem().Name.ShouldBe("Work");
    }

    [Theory]
    [InlineData("Work")]
    [InlineData("Personal")]
    [InlineData("Shopping")]
    [InlineData("University")]
    public async Task CreateCategory_ValidName_CreatedReturnedAndCategoryInDatabase(string categoryName)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/Category", new CreateCategoryRequest(categoryName));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var returned = await response.Content.ReadFromJsonAsync<Category>();
        returned.ShouldNotBeNull();
        returned.Name.ShouldBe(categoryName);

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem().Name.ShouldBe(categoryName);
    }

    [Theory]
    [InlineData("  Work  ", "Work")]
    [InlineData("\tPersonal\n", "Personal")]
    public async Task CreateCategory_NameHasSurroundingWhitespace_TrimmedNameStored(
        string input, string expected)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/Category", new CreateCategoryRequest(input));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem().Name.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Work", "Work")]
    [InlineData("Work", "work")]
    [InlineData("Work", "WORK")]
    [InlineData("Work", "  work  ")]
    public async Task CreateCategory_NameMatchesExistingIgnoringCase_OkReturnedAndNoDuplicateCreated(
        string first, string second)
    {
        (await HttpClient.PostAsJsonAsync("/api/Category", new CreateCategoryRequest(first)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await HttpClient.PostAsJsonAsync(
            "/api/Category", new CreateCategoryRequest(second));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = await response.Content.ReadFromJsonAsync<Category>();
        returned.ShouldNotBeNull();
        returned.Name.ShouldBe(first); // the originally stored casing wins

        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CreateCategory_NameExistsOnAnotherUsersTask_ExistingCategoryReused()
    {
        var otherUserId = await AddOtherUser();
        await AddCategoriesViaTask(1, otherUserId); // creates "Category 0"

        var response = await HttpClient.PostAsJsonAsync(
            "/api/Category", new CreateCategoryRequest("Category 0"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task CreateCategory_BlankName_BadRequestReturnedAndNothingCreated(string categoryName)
    {
        var response = await HttpClient.PostAsJsonAsync(
            "/api/Category", new CreateCategoryRequest(categoryName));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetCategories_CategoryCreatedButNotAttachedToAnyTask_NotReturned()
    {
        await HttpClient.PostAsJsonAsync("/api/Category", new CreateCategoryRequest("Work"));

        var response = await HttpClient.GetAsync("/api/Category");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var returned = await response.Content.ReadFromJsonAsync<ICollection<Category>>();
        returned.ShouldNotBeNull();
        returned.ShouldBeEmpty();

        // the row exists, it just isn't reachable from this user's tasks
        await using var context = await DbContextFactory.CreateDbContextAsync();
        context.Category.ShouldHaveSingleItem().Name.ShouldBe("Work");
    }
}
