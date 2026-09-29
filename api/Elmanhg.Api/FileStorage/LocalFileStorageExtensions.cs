using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using Elmanhg.Infrastructure.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.FileStorage;

public static class LocalFileStorageExtensions
{
    public static WebApplication UseLocalFileStorage(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        if (options.Provider != FileStorageProvider.Local)
        {
            return app;
        }

        var root = Path.GetFullPath(options.LocalRootPath, app.Environment.ContentRootPath);
        Directory.CreateDirectory(root);
        var files = new PhysicalFileProvider(root);
        app.UseMiddleware<TeacherThreadMediaMiddleware>(files, new PathString(options.PublicBaseUrl));
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PublicMediaFileProvider(files, [TeacherThreadImageFormats.StorageFolder]),
            RequestPath = options.PublicBaseUrl,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
            },
        });
        return app;
    }
}
