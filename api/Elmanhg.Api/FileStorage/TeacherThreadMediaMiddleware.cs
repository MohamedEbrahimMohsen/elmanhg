using Elmanhg.Application.TeacherThreads.CanViewTeacherThreadImage;
using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using MediatR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Elmanhg.Api.FileStorage;

public sealed class TeacherThreadMediaMiddleware(RequestDelegate next, PhysicalFileProvider files, PathString mediaPath)
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private readonly PathString _imagesPath = mediaPath.Add($"/{TeacherThreadImageFormats.StorageFolder}");

    public async Task InvokeAsync(HttpContext context, ISender sender)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !context.Request.Path.StartsWithSegments(_imagesPath, StringComparison.Ordinal, out var fileName))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var file = files.GetFileInfo($"{TeacherThreadImageFormats.StorageFolder}{fileName}");
        var canView = await sender.Send(new CanViewTeacherThreadImageQuery(context.Request.Path.Value!), context.RequestAborted).ConfigureAwait(false);
        if (!canView || !file.Exists || file.IsDirectory)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = ContentTypes.TryGetContentType(file.Name, out var contentType) ? contentType : "application/octet-stream";
        context.Response.ContentLength = file.Length;
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, no-store";
        await context.Response.SendFileAsync(file, context.RequestAborted).ConfigureAwait(false);
    }
}
