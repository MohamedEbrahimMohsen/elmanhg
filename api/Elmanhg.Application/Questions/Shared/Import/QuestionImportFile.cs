using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportFile
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string TemplateFileName = "elmanhg-question-import-template.xlsx";
    public static readonly IReadOnlyList<string> Extensions = [".xlsx"];

    // An .xlsx workbook is a zip archive: every one starts with a local file header.
    private const int ZipSignatureLength = 4;

    private static ReadOnlySpan<byte> ZipSignature => [0x50, 0x4B, 0x03, 0x04];

    public static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    public static bool HasZipSignature(IFormFile file)
    {
        Span<byte> buffer = stackalloc byte[ZipSignatureLength];
        using var stream = file.OpenReadStream();
        var header = buffer[..stream.ReadAtLeast(buffer, ZipSignatureLength, throwOnEndOfStream: false)];
        return header.SequenceEqual(ZipSignature);
    }

    public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
