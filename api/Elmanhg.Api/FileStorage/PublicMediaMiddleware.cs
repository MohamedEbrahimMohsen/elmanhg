using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Application.TrainingExports.Shared;

namespace Elmanhg.Api.FileStorage;

public sealed class PublicMediaMiddleware(RequestDelegate next, PathString mediaPath)
{
    // Keys are random and never rewritten, so a public copy can be cached for a year.
    private const string PublicCacheControl = "public, max-age=31536000, immutable";

    public async Task InvokeAsync(HttpContext context, IFileStorage fileStorage)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !context.Request.Path.StartsWithSegments(mediaPath, StringComparison.Ordinal, out var rest))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var key = rest.Value!.TrimStart('/');
        if (!IsPublicKey(key))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using var file = await fileStorage.OpenReadAsync(key, context.RequestAborted).ConfigureAwait(false);
        if (file is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = file.ContentType;
        context.Response.ContentLength = file.Length;
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = PublicCacheControl;
        await file.Content.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    internal static bool IsPublicKey(string key)
    {
        if (key.Length == 0 || !key.All(IsKeyCharacter))
        {
            return false;
        }

        var segments = key.Split('/');
        return segments.All(x => x.Length > 0 && x != "." && x != "..") && !IsPrivateFolder(segments[0].TrimEnd('.'));
    }

    private static bool IsPrivateFolder(string folder) => string.Equals(folder, TeacherThreadImageFormats.StorageFolder, StringComparison.OrdinalIgnoreCase) || string.Equals(folder, TrainingExportFiles.StorageFolder, StringComparison.OrdinalIgnoreCase);

    private static bool IsKeyCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '/' or '-';
}
