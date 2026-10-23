using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Models.DTOs;
using Grinder.Api.Utilities;

namespace Grinder.Api.Controllers;

/// <summary>
/// Controller responsible for handling Task-related API requests.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    /// <summary>
    /// Constructs a new TasksController.
    /// </summary>
    /// <param name="taskService">Service used to manage task operations.</param>
    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    /// <summary>
    /// Retrieves all tasks.
    /// </summary>
    /// <returns>A list of all task items.</returns>
    [HttpGet("user/{requestedUserId:int?}")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasks(int? requestedUserId)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        var tasks = await _taskService.GetAllTasksAsync(userId.Value, requestedUserId);
        var response = tasks.Select(t => TaskResponse.FromEntity(t, userId.Value));
        return Ok(response);
    }

    /// <summary>
    /// Retrieves all tasks.
    /// </summary>
    /// <returns>A list of all task items.</returns>
    [HttpGet("parents")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetValidParentTasks()
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        var response = await _taskService.GetAllValidParentTasksAsync(userId.Value);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves a specific task by its ID.
    /// </summary>
    /// <param name="id">The ID of the task.</param>
    /// <returns>The requested task if found; otherwise 404.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TaskItem>> GetTask(int id)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        var task = await _taskService.GetTaskByIdAsync(userId.Value, id);
        if (task == null)
            return NotFound();

        var response = TaskResponse.FromEntity(task, userId.Value);
        return Ok(response);
    }

    /// <summary>
    /// Creates a new task.
    /// </summary>
    /// <param name="taskItem">
    /// The task details including Title, Description, ParentTaskId, Due date, Due Time and Status.
    /// </param>
    /// <returns>
    /// Returns the newly created task.
    /// </returns>
    [HttpPost]
    public async Task<ActionResult<TaskItem>> CreateTask([FromBody] CreateTaskRequest request)
    {

        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var newTask = await _taskService.CreateNewTaskAsync(userId.Value, request);
            var response = TaskResponse.FromEntity(newTask, userId.Value);
            return CreatedAtAction(nameof(GetTask), new { id = newTask.Id }, response);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? "Request", ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("Request", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Edits an existing task
    /// </summary>
    /// <param name="id">The ID of the task to edit</param>
    /// <param name="body">The fields to update</param>
    /// <returns>The updated task if successful; otherwise an error response.</returns>
    /// <remarks>
    /// - At least one field must be provided to update, else a 400 Bad Request is returned.
    /// </remarks>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<TaskResponse>> PatchTask(int id, [FromBody] PatchTaskRequest body)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var updatedTask = await _taskService.PatchTaskAsync(
                userId.Value,
                id,
                body
            );

            if (updatedTask == null)
            {
                return NotFound();
            }

            var response = TaskResponse.FromEntity(updatedTask, userId.Value);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? "Request", ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Deletes a task and its nested child tasks.
    /// </summary>
    /// <param name="id">The task ID.</param>
    /// <returns>Status code 204 when deleted, or 404 when the task does not exist.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        var deleted = await _taskService.DeleteTaskAsync(userId.Value, id);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>
    /// Edits the order attribute of all tasks in a task list and updates parent id if necessary
    /// </summary>
    /// <param name="items">Contains the tasks id and order</param>
    /// <returns>
	/// Status Code Ok
	/// </returns>
    [HttpPatch("reorder")]
    public async Task<IActionResult> ReorderTasks([FromBody] List<ReorderTasksRequest> items)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        await _taskService.ReorderTasks(items, userId.Value);
        return Ok();
    }

    /// <summary>
    /// Retrieves a count of a users current task statuses for all visible tasks depending on the relationship with the user.
    /// </summary>
    /// <param name="requestedUserId">The ID of the user whos statuses are being requested</param>
    /// <returns>The status counts or zeros</returns>
    [HttpGet("status-count/{requestedUserId:int}")]
    public async Task<ActionResult<StatusCountResponse>> GetStatusCount(int requestedUserId)
    {
        var requestingUserId = Session.GetUserId(HttpContext);
        if (requestingUserId == null)
        {
            return Unauthorized();
        }

        var statusCounts = await _taskService.GetTaskStatusCountsAsync(requestingUserId.Value, requestedUserId);
        return Ok(statusCounts);
    }

}
