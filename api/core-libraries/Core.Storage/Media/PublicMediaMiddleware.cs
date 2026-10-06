using Microsoft.AspNetCore.Http;

namespace Core.Storage.Media;

public sealed class PublicMediaMiddleware(RequestDelegate next, PublicMediaOptions options)
{
    public async Task InvokeAsync(HttpContext context, IFileStorage fileStorage)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !context.Request.Path.StartsWithSegments(options.RequestPath, StringComparison.Ordinal, out var rest))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var key = rest.Value!.TrimStart('/');
        if (!IsPublicKey(key, options.PrivateFolders))
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

        await context.Response.WriteStoredFileAsync(file, MediaCacheControl.PublicImmutable).ConfigureAwait(false);
    }

    public static bool IsPublicKey(string key, IReadOnlyList<string> privateFolders)
    {
        if (key.Length == 0 || !key.All(IsKeyCharacter))
        {
            return false;
        }

        var segments = key.Split('/');
        return segments.All(x => x.Length > 0 && x != "." && x != "..") && !privateFolders.Any(folder => string.Equals(folder, segments[0].TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKeyCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '/' or '-';
}
