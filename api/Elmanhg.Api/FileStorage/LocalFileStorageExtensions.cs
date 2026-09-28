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
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(root),
            RequestPath = options.PublicBaseUrl,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
            },
        });
        return app;
    }
}
