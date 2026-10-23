using System.Net.Http.Headers;
using System.Text;
using Grinder.Api.Models.Attachments;

namespace Grinder.Api.Services;
public interface IAttachmentValidator
{
    Task<ValidatedAttachment> ValidateAsync(IFormFile file, AttachmentKind? allowedKind = null);
}

public sealed class AttachmentValidator : IAttachmentValidator
{
    private const long MAX_IMAGE_FILE_SIZE = 5L * 1024 * 1024;
    private const long MAX_VIDEO_FILE_SIZE = 50L * 1024 * 1024;
    
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];                                                                        
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];                  
    private static readonly byte[] Gif87aSignature = "GIF87a"u8.ToArray();              
    private static readonly byte[] Gif89aSignature = "GIF89a"u8.ToArray();              
    private static readonly byte[] IsoBmffFtypSignature = "ftyp"u8.ToArray(); // shared by MP4 + MOV containers                                                             
    private static readonly byte[] WebmSignature = [0x1A, 0x45, 0xDF, 0xA3];

    private const int IsoBmffFtypeOffset = 4;

    private const int SIGNATURE_PEEK_BYTES = 16;

    private static readonly IReadOnlyList<AttachmentRule> Rules =
    [
        new AttachmentRule(
            AttachmentKind.Image,
            AttachmentFormat.Png,
            "image/png",
            ExtensionSet(".png"),
            MAX_IMAGE_FILE_SIZE,
            [new FileSignature(0, PngSignature)]
        ),
        new AttachmentRule(
            AttachmentKind.Image,
            AttachmentFormat.Jpeg,
            "image/jpeg",
            ExtensionSet(".jpg", ".jpeg"),
            MAX_IMAGE_FILE_SIZE,
            [new FileSignature(0, JpegSignature)]
        ),
        new AttachmentRule(
            AttachmentKind.Image,
            AttachmentFormat.Svg,
            "image/svg+xml",
            ExtensionSet(".svg"),
            MAX_IMAGE_FILE_SIZE,
           []
        ),
        new AttachmentRule(
            AttachmentKind.Image,
            AttachmentFormat.Gif,
            "image/gif",
            ExtensionSet(".gif"),
            MAX_IMAGE_FILE_SIZE,
            [new FileSignature(0, Gif87aSignature), new FileSignature(0, Gif89aSignature)]
        ),
        new AttachmentRule(
            AttachmentKind.Video,
            AttachmentFormat.Mp4,
            "video/mp4",
            ExtensionSet(".mp4"),
            MAX_VIDEO_FILE_SIZE,
            [new FileSignature(IsoBmffFtypeOffset, IsoBmffFtypSignature)]
        ),
        new AttachmentRule(
            AttachmentKind.Video,
            AttachmentFormat.Mov,
            "video/quicktime",
            ExtensionSet(".mov"),
            MAX_VIDEO_FILE_SIZE,
            [new FileSignature(IsoBmffFtypeOffset, IsoBmffFtypSignature)]
        ),
        new AttachmentRule(
            AttachmentKind.Video,
            AttachmentFormat.Webm,
            "video/webm",
            ExtensionSet(".webm"),
            MAX_VIDEO_FILE_SIZE,
            [new FileSignature(0, WebmSignature)]
        )
    ];

    public async Task<ValidatedAttachment> ValidateAsync(IFormFile file, AttachmentKind? allowedKind = null)
    {
        if (file == null || file.Length == 0)
        {
            throw new AttachmentValidationException(AttachmentValidationError.EmptyFile);
        }
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string contentType = ParseContentType(file.ContentType);

        AttachmentRule? rule = Rules.FirstOrDefault(candidate =>
            string.Equals(candidate.ContentType, contentType, StringComparison.OrdinalIgnoreCase) &&
            candidate.Extensions.Contains(extension)
        );

        if (rule == null)
        {
            AttachmentValidationError error = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
                ? AttachmentValidationError.UnsupportedVideoType
                : AttachmentValidationError.UnsupportedImageType;
            throw new AttachmentValidationException(error);
        }

        if (rule.Format == AttachmentFormat.Svg)
        {
            if (!await LooksLikeSvgAsync(file))
            {
                throw new AttachmentValidationException(AttachmentValidationError.ContentMismatch);
            }
        }
        else
        {
            byte[] header = await ReadHeaderAsync(file);
            if (!MatchesAnySignature(header, rule.FileSignatures))
                throw new AttachmentValidationException(AttachmentValidationError.ContentMismatch);
        }

        if (allowedKind.HasValue && rule.Kind != allowedKind.Value)
        {
            AttachmentValidationError error = allowedKind.Value == AttachmentKind.Image
                ? AttachmentValidationError.UnsupportedImageType
                : AttachmentValidationError.UnsupportedVideoType;
            throw new AttachmentValidationException(error);
        }

        if (file.Length > rule.MaxSize)
        {
            AttachmentValidationError error = rule.Kind == AttachmentKind.Image
                ? AttachmentValidationError.ImageTooLarge
                : AttachmentValidationError.VideoTooLarge;
            throw new AttachmentValidationException(error);
        }
        
        return new ValidatedAttachment(rule.Kind, rule.Format, rule.ContentType, extension, file.Length);
    }

    private static string ParseContentType(string contentType)
    {
        bool parsedSuccessfully = MediaTypeHeaderValue.TryParse(
            contentType,
            out MediaTypeHeaderValue? parsed
        );

        if (!parsedSuccessfully || string.IsNullOrWhiteSpace(parsed?.MediaType))
        {
            return string.Empty;
        }

        return parsed.MediaType.ToLowerInvariant();
    }

    private static async Task<byte[]> ReadHeaderAsync(IFormFile file)
    {
        int count = (int)Math.Min(SIGNATURE_PEEK_BYTES, file.Length);
        byte[] buffer = new byte[count];
        
        await using Stream stream = file.OpenReadStream();
        await stream.ReadExactlyAsync(buffer);
        stream.Position = 0;

        return buffer;
    }

    private static bool MatchesAnySignature(byte[] header, IReadOnlyList<FileSignature> fileSignatures)
    {
        if (fileSignatures.Count == 0)
        {
            return true;    // formats w no byte signatures (svg's) skip this check
        }

        return fileSignatures.Any(signature =>
            header.Length >= signature.Offset + signature.Bytes.Length &&
            header.AsSpan(signature.Offset, signature.Bytes.Length).SequenceEqual(signature.Bytes)
        );
    }

    private static async Task<bool> LooksLikeSvgAsync(IFormFile file)
    {
        await using Stream stream = file.OpenReadStream();
        using StreamReader reader = new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true
        );

        char[] buffer = new char[512];
        int read = await reader.ReadAsync(buffer, 0, buffer.Length);
        stream.Position = 0;

        string head = new string(buffer, 0, read).TrimStart(['﻿', ' ', '\t', '\r', '\n']);
        return head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> ExtensionSet(params string[] extensions)
    {
        return new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
    }
}
