namespace Grinder.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grinder.Api.Models.Attachments;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Grinder.Api.Utilities;


[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AttachmentUploadsController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentUploadsController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    [HttpPost]
    [RequestSizeLimit(57_671_680)] // 55MB
    public async Task<ActionResult<TaskAttachment>> Upload(IFormFile file, [FromForm] int? width, [FromForm] int? height, CancellationToken cancellationToken)
    {
        int? userId = Session.GetUserId(HttpContext);

        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            TaskAttachment attachment = await _attachmentService.UploadTemporaryAsync(
                userId.Value,
                file,
                width,
                height,
                cancellationToken
            );

            return Ok(attachment);
        }
        catch (AttachmentValidationException exception)
        {
            return BadRequest(new
            {
                code = exception.Error.ToString(),
                message = exception.Message
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{attachmentId:int}")]
    public async Task<ActionResult> Delete(int attachmentId, CancellationToken cancellationToken)
    {
        int? userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }
        bool deleted = await _attachmentService.DeleteAsync(
            userId.Value,
            attachmentId,
            cancellationToken
        );
        return deleted ? NoContent() : NotFound();
    }
}
