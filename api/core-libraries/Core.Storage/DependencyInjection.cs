using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Core.Storage.Local;
using Core.Storage.Media;
using Core.Storage.S3;
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
        services.AddSingleton<IAmazonS3>(serviceProvider => CreateS3Client(Options(serviceProvider)));
        services.AddScoped<LocalDiskFileStorage>();
        services.AddScoped<S3FileStorage>();
        services.AddScoped<IFileStorage>(serviceProvider => Options(serviceProvider).Provider switch
        {
            FileStorageProvider.Local => serviceProvider.GetRequiredService<LocalDiskFileStorage>(),
            FileStorageProvider.S3 => serviceProvider.GetRequiredService<S3FileStorage>(),
            _ => throw new InvalidOperationException("Unsupported FileStorage:Provider."),
        });
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

    private static AmazonS3Client CreateS3Client(FileStorageOptions options)
    {
        var config = new AmazonS3Config
        {
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (string.IsNullOrWhiteSpace(options.S3ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.S3Region);
        }
        else
        {
            config.ServiceURL = options.S3ServiceUrl;
            config.AuthenticationRegion = options.S3Region;
            config.ForcePathStyle = options.S3ForcePathStyle;
        }

        return new AmazonS3Client(new BasicAWSCredentials(options.S3AccessKeyId, options.S3SecretAccessKey), config);
    }

    private static FileStorageOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value;
}
