using Core.Storage;
using Core.Storage.Media;
using Elmanhg.Application.TeacherThreads.CanViewTeacherThreadMedia;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using MediatR;

namespace Elmanhg.Api.FileStorage;

public sealed class TeacherThreadMediaMiddleware(RequestDelegate next, PathString mediaPath)
{
    private readonly PathString _privatePath = mediaPath.Add($"/{TeacherThreadImageFormats.StorageFolder}");

    public async Task InvokeAsync(HttpContext context, ISender sender, IFileStorage fileStorage)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !context.Request.Path.StartsWithSegments(_privatePath, StringComparison.Ordinal, out var fileName))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var canView = await sender.Send(new CanViewTeacherThreadMediaQuery(context.Request.Path.Value!), context.RequestAborted).ConfigureAwait(false);
        if (!canView)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using var file = await fileStorage.OpenReadAsync($"{TeacherThreadImageFormats.StorageFolder}{fileName}", context.RequestAborted).ConfigureAwait(false);
        if (file is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await context.Response.WriteStoredFileAsync(file, MediaCacheControl.PrivateNoStore).ConfigureAwait(false);
    }
}
