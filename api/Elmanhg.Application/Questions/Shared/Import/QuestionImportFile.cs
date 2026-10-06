using Core.Validation.Files;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportFile
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string TemplateFileName = "elmanhg-question-import-template.xlsx";
    public static readonly IReadOnlyList<string> Extensions = [".xlsx"];

    // An .xlsx workbook is a zip archive: every one starts with a local file header.
    public static readonly IReadOnlyList<FileSignature> Signatures = [FileSignature.Zip.ForExtensions(".xlsx")];

    public static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    public static bool HasZipSignature(IFormFile file) => FileSignature.Matches(file, Signatures);

    public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
