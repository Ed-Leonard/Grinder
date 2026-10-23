using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Services;
using Grinder.Api.Utilities;

namespace Grinder.Api.Controllers;

/// <summary>
/// Controller responsible for handling Search-related API requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    /// <summary>
    /// Constructs a new SearchController.
    /// </summary>
    /// <param name="searchService">Service used to manage search operations.</param>
    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }


    /// <summary>
    /// Retrieves search results based on the provided criteria.
    /// </summary>
    /// <returns>A list of all task items.</returns>
    [HttpGet]
    public async Task<ActionResult<SearchResponseDto>> GetSearchResults([FromQuery] string query, [FromQuery] int page =1, [FromQuery]int limit = 20)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        var response = await _searchService.GetResultsAsync(userId.Value, query, page, limit);
        return Ok(response);
    }
}
