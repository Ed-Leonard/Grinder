namespace Grinder.Api.Tests.Unit;

using Microsoft.AspNetCore.Http;
using Grinder.Api.Models.Attachments;
using Grinder.Api.Services;
using Shouldly;

public class AttachmentValidatorTest
{
    private readonly AttachmentValidator _attachmentValidator = new();

    [Theory]
    [InlineData("photo.png", "image/png", AttachmentFormat.Png, ".png")]
    [InlineData("photo.jpg", "image/jpeg", AttachmentFormat.Jpeg, ".jpg")]
    [InlineData("photo.jpeg", "image/jpeg", AttachmentFormat.Jpeg, ".jpeg")]
    [InlineData("photo.svg", "image/svg+xml", AttachmentFormat.Svg, ".svg")]
    [InlineData("photo.gif", "image/gif", AttachmentFormat.Gif, ".gif")]
    public async Task Validate_SupportedImage_ReturnsValidatedImage(string filename, string contentType,
        AttachmentFormat expectedFormat, string expectedExtension)
    {
        IFormFile file = CreateFile(filename, contentType, 1024);

        ValidatedAttachment result = await _attachmentValidator.ValidateAsync(file);

        result.Kind.ShouldBe(AttachmentKind.Image);
        result.Format.ShouldBe(expectedFormat);
        result.ContentType.ShouldBe(contentType);
        result.Extension.ShouldBe(expectedExtension);
        result.FileSize.ShouldBe(1024);
    }

    [Theory]
    [InlineData("clip.mp4", "video/mp4", AttachmentFormat.Mp4)]
    [InlineData("clip.mov", "video/quicktime", AttachmentFormat.Mov)]
    [InlineData("clip.webm", "video/webm", AttachmentFormat.Webm)]
    public async Task Validate_SupportedVideo_ReturnsValidatedVideo(string filename, string contentType, AttachmentFormat expectedFormat)
    {
        IFormFile file = CreateFile(filename, contentType, 1024);

        ValidatedAttachment result = await _attachmentValidator.ValidateAsync(file);

        result.Kind.ShouldBe(AttachmentKind.Video);
        result.Format.ShouldBe(expectedFormat);
    }

    [Fact]
    public async Task Validate_UppercaseExtension_NormalisesExtension()
    {
        IFormFile file = CreateFile("PHOTO.PNG", "image/png", 1024);

        ValidatedAttachment result = await _attachmentValidator.ValidateAsync(file);

        result.Extension.ShouldBe(".png");
        result.Format.ShouldBe(AttachmentFormat.Png);
    }

    [Fact]
    public async Task Validate_ContentTypeWithParameters_AcceptsFile()
    {
        IFormFile file = CreateFile(
            "photo.png",
            "image/png; charset=binary",
            1024
        );

        ValidatedAttachment result = await _attachmentValidator.ValidateAsync(file);

        result.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task ValidateEmptyFile_ThrowsEmptyFileError()
    {
        IFormFile file = CreateFile("photo.png", "image/png", 0);

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(AttachmentValidationError.EmptyFile);
        exception.Message.ShouldBe("Please choose a file to upload");
    }

    [Theory]
    [InlineData("photo.webp", "image/webp")]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("photo.bmp", "image/bmp")]
    public async Task Validate_UnsupportedImage_ThrowsUnsupportedImageError(
        string fileName,
        string contentType)
    {
        IFormFile file = CreateFile(fileName, contentType, 1024);

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(
            AttachmentValidationError.UnsupportedImageType
        );
        exception.Message.ShouldBe(
            "The image must be of type png, jpg, svg, or gif"
        );
    }

    [Fact]
    public async Task Validate_UnsupportedVideo_ThrowsUnsupportedVideoError()
    {
        IFormFile file = CreateFile("clip.avi", "video/x-msvideo", 1024);

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(
            AttachmentValidationError.UnsupportedVideoType
        );
        exception.Message.ShouldBe(
            "The video must be of type mp4, mov, or webm"
        );
    }

    [Fact]
    public async Task Validate_VideoOverFiftyMegabytes_ThrowsVideoTooLargeError()
    {
        const long fileSize = 50L * 1024 * 1024 + 1;
        IFormFile file = CreateFile("clip.mp4", "video/mp4", fileSize);

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(AttachmentValidationError.VideoTooLarge);
        exception.Message.ShouldBe(
            "Video is too large, please choose a smaller file"
        );
    }

    [Fact]
    public async Task Validate_ImageOverFiveMegabytes_ThrowsImageTooLargeError()
    {
        const long fileSize = 5L * 1024 * 1024 + 1;
        IFormFile file = CreateFile("photo.png", "image/png", fileSize);

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(AttachmentValidationError.ImageTooLarge);
        exception.Message.ShouldBe(
            "Image is too large, please choose a smaller file"
        );
    }

    [Fact]
    public async Task Validate_RenamedFileWithWrongSignature_ThrowsContentMismatchError()
    {
        // Real content is a JPEG, but it's disguised as a PNG via extension + content-type.
        IFormFile file = CreateFile("photo.png", "image/png", 1024, ContentBytesFor("photo.jpg"));

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(AttachmentValidationError.ContentMismatch);
    }

    [Fact]
    public async Task Validate_SvgWithoutSvgContent_ThrowsContentMismatchError()
    {
        IFormFile file = CreateFile("photo.svg", "image/svg+xml", 1024, "not xml at all"u8.ToArray());

        AttachmentValidationException exception =
            await Should.ThrowAsync<AttachmentValidationException>(
                () => _attachmentValidator.ValidateAsync(file)
            );

        exception.Error.ShouldBe(AttachmentValidationError.ContentMismatch);
    }

    private static IFormFile CreateFile(
        string fileName,
        string contentType,
        long fileSize,
        byte[]? content = null)
    {
        MemoryStream stream = new(content ?? ContentBytesFor(fileName));

        FormFile file = new(
            stream,
            0,
            fileSize,
            "file",
            fileName
        )
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return file;
    }

    // Builds real magic-number bytes for the given file's extension so the byte-signature
    // check in AttachmentValidator has genuine content to match against, padded to at least
    // 16 bytes since that's how many the validator peeks at.
    private static byte[] ContentBytesFor(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".png" => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 0),
            ".jpg" or ".jpeg" => Pad([0xFF, 0xD8, 0xFF], 0),
            ".gif" => Pad("GIF89a"u8.ToArray(), 0),
            ".svg" => "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray(),
            ".mp4" or ".mov" => Pad("ftyp"u8.ToArray(), 4),
            ".webm" => Pad([0x1A, 0x45, 0xDF, 0xA3], 0),
            _ => [1, 2, 3, 4]
        };
    }

    private static byte[] Pad(byte[] signature, int offset)
    {
        byte[] buffer = new byte[16];
        signature.CopyTo(buffer, offset);
        return buffer;
    }
}
