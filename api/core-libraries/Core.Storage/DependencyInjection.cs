using Core.Storage.Local;
using Core.Storage.Media;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Core.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreFileStorage(this IServiceCollection services)
    {
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<FileStorageOptions>, FileStorageOptionsValidator>();
        services.AddKeyedScoped<IFileStorage, LocalDiskFileStorage>(FileStorageProvider.Local);
        services.AddScoped<IFileStorage>(serviceProvider => Resolve(serviceProvider));
        return services;
    }

    public static IApplicationBuilder UseCorePublicMedia(this IApplicationBuilder app, IReadOnlyList<string> privateFolders)
    {
        var storage = app.ApplicationServices.GetRequiredService<IOptions<FileStorageOptions>>().Value;
        var options = new PublicMediaOptions(new PathString(storage.PublicBaseUrl), privateFolders);
        if (storage.Provider == FileStorageProvider.S3)
        {
            app.UseMiddleware<PublicMediaMiddleware>(options);
            return app;
        }

        var root = storage.ResolveLocalRoot(app.ApplicationServices.GetRequiredService<IHostEnvironment>().ContentRootPath);
        Directory.CreateDirectory(root);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PublicMediaFileProvider(new PhysicalFileProvider(root), privateFolders),
            RequestPath = options.RequestPath,
            OnPrepareResponse = context => context.Context.Response.SetMediaHeaders(MediaCacheControl.PublicImmutable),
        });
        return app;
    }

    private static IFileStorage Resolve(IServiceProvider serviceProvider)
    {
        var provider = Options(serviceProvider).Provider;
        return serviceProvider.GetKeyedService<IFileStorage>(provider) ?? throw new InvalidOperationException($"No file storage is registered for FileStorage:Provider '{provider}'.");
    }

    private static FileStorageOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
}
