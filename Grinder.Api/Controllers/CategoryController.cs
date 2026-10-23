using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Grinder.Api.Services;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;
namespace Grinder.Api.Controllers;

/// <summary>
/// Provides endpoints for managing task categories.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    /// <summary>
    /// Creates a new <see cref="CategoryController"/>.
    /// </summary>
    /// <param name="categoryService">
    /// Service used to retrieve and create categories.
    /// </param>
    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Retrieves all categories which the user has for their tasks.
    /// </summary>
    /// <returns>
    /// A list of categories.
    /// </returns>
    /// <response code="200">The categories were successfully retrieved.</response>
    /// <response code="401">The user is not authenticated.</response>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskCategoryResponse>>> GetCategories()
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }


        var categories = await _categoryService.GetCategoriesForUserAsync(userId.Value);
        return Ok(categories);
    }

    /// <summary>
    /// Creates a new category.
    /// </summary>
    /// <param name="request">
    /// DTO entity containing the category name
    /// </param>
    /// <returns>
    /// Returns a 201 Created response if the category is successfully created.
    /// </returns>
    /// <response code="201">The category was successfully created.</response>
    /// <response code="400">The request was invalid or the category could not be created.</response>
    [HttpPost]
    public async Task<ActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var (category, wasCreated) = await _categoryService.AddCategory(request.Name);

            return wasCreated
                ? StatusCode(StatusCodes.Status201Created, category)
                : StatusCode(StatusCodes.Status200OK, category);
        }
        catch (ArgumentException e)
        {
            return BadRequest(e.Message);
        }

    }
}
