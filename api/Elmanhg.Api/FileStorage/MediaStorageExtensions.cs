using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.FileStorage;

public static class MediaStorageExtensions
{
    public static WebApplication UseMediaStorage(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var mediaPath = new PathString(options.PublicBaseUrl);
        app.UseMiddleware<TeacherThreadMediaMiddleware>(mediaPath);
        if (options.Provider == FileStorageProvider.S3)
        {
            app.UseMiddleware<PublicMediaMiddleware>(mediaPath);
            return app;
        }

        var root = Path.GetFullPath(options.LocalRootPath, app.Environment.ContentRootPath);
        Directory.CreateDirectory(root);
        var files = new PhysicalFileProvider(root);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PublicMediaFileProvider(files, [TeacherThreadImageFormats.StorageFolder]),
            RequestPath = options.PublicBaseUrl,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                // Public media keys are write-once GUID paths, so a stored URL never serves different bytes.
                context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            },
        });
        return app;
    }
}
