using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Storage.S3;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreS3FileStorage(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3>(serviceProvider => CreateS3Client(serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>().Value));
        services.AddKeyedScoped<IFileStorage, S3FileStorage>(FileStorageProvider.S3);
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
}
