using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Elmanhg.Application.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Storage;

public static class FileStorageServiceCollectionExtensions
{
    public static IServiceCollection AddFileStorage(this IServiceCollection services)
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
