using Microsoft.AspNetCore.Http;

namespace Core.Storage.Media;

public static class StoredFileResponseExtensions
{
    public static void SetMediaHeaders(this HttpResponse response, string cacheControl)
    {
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.CacheControl = cacheControl;
    }

    public static async Task WriteStoredFileAsync(this HttpResponse response, StoredFile file, string cacheControl)
    {
        response.ContentType = file.ContentType;
        response.ContentLength = file.Length;
        response.SetMediaHeaders(cacheControl);
        await file.Content.CopyToAsync(response.Body, response.HttpContext.RequestAborted).ConfigureAwait(false);
    }
}
