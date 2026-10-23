namespace Grinder.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Grinder.Api.Models.Attachments;
using Grinder.Api.Services;
using Grinder.Api.Utilities;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAttachmentService _attachmentService;

    public MediaController(IUserService userService, IAttachmentService attachmentService)
    {
        _userService = userService;
        _attachmentService = attachmentService;
    }

    [HttpGet("{userId:int}/profile-picture")]
    public async Task<IActionResult> GetProfilePicture(
        CancellationToken cancellationToken, [FromRoute] int userId
    )
    {
        StoredFile? file = await _userService.GetProfilePictureUrlAsync(userId, cancellationToken);

        if (file is null)
        {
            return NotFound();
        }

        return PhysicalFile(
            file.PhysicalPath,
            file.ContentType,
            lastModified: null,
            entityTag: new EntityTagHeaderValue($"\"{file.Version}\"")
        );
    }

    [HttpGet("task-attachments/{attachmentId:int}")]
    public async Task<IActionResult> GetTaskAttachment(
    int attachmentId,
    CancellationToken cancellationToken
)
    {
        var userId = Session.GetUserId(HttpContext);
        if (userId == null)
        {
            return Unauthorized();
        }

        StoredFile? file = await _attachmentService.GetTaskAttachmentFileAsync(
            userId.Value,
            attachmentId,
            cancellationToken
        );

        if (file is null)
        {
            return NotFound();
        }

        EntityTagHeaderValue etag = new($"\"{file.Version}\"");

        return PhysicalFile(file.PhysicalPath, file.ContentType, null, etag);
    }
}
