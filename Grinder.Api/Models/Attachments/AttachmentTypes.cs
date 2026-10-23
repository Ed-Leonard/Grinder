namespace Grinder.Api.Models.Attachments;

public enum AttachmentKind
{
    Image,
    Video    
}

public enum AttachmentFormat
{
    Png,
    Jpeg,
    Svg,
    Gif,
    Mp4,
    Mov,
    Webm
}

public enum AttachmentValidationError
{
    EmptyFile,
    UnsupportedImageType,
    UnsupportedVideoType,
    ImageTooLarge,
    VideoTooLarge,
    ContentMismatch
}

public readonly record struct ValidatedAttachment(
    AttachmentKind Kind,
    AttachmentFormat Format,
    string ContentType,
    string Extension,
    long FileSize
);

public sealed record FileSignature(int Offset, byte[] Bytes);

public sealed record AttachmentRule(
    AttachmentKind Kind,
    AttachmentFormat Format,
    string ContentType,
    IReadOnlySet<string> Extensions,
    long MaxSize,
    IReadOnlyList<FileSignature> FileSignatures
);    
