namespace Grinder.Api.Models.Attachments;

public sealed class AttachmentValidationException : Exception
{
    public AttachmentValidationError Error { get; }
    
    public AttachmentValidationException(AttachmentValidationError error) : base(GetMessage(error))
    {
        Error = error;
    }

    private static string GetMessage(AttachmentValidationError error)
    {
        return error switch
        {
            AttachmentValidationError.EmptyFile => "Please choose a file to upload",

            AttachmentValidationError.UnsupportedImageType => "The image must be of type png, jpg, svg, or gif",

            AttachmentValidationError.UnsupportedVideoType => "The video must be of type mp4, mov, or webm",

            AttachmentValidationError.ImageTooLarge => "Image is too large, please choose a smaller file",

            AttachmentValidationError.VideoTooLarge => "Video is too large, please choose a smaller file",

            _ => "The attachment is invalid"
        };
    }
}   
