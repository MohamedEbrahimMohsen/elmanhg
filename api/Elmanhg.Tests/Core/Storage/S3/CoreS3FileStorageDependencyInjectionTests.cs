using Core.Storage;
using Core.Storage.Local;
using Core.Storage.S3;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Core.Storage.S3;

public sealed class CoreS3FileStorageDependencyInjectionTests
{
    [Fact]
    public void AddCoreS3FileStorage_S3Provider_ResolvesS3FileStorage()
    {
        using var provider = BuildProvider(S3Settings("S3"), withS3: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFileStorage>().Should().BeOfType<S3FileStorage>();
    }

    [Fact]
    public void AddCoreS3FileStorage_LocalProvider_ResolvesLocalDiskFileStorage()
    {
        using var provider = BuildProvider(S3Settings("Local"), withS3: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFileStorage>().Should().BeOfType<LocalDiskFileStorage>();
    }

    private static Dictionary<string, string?> S3Settings(string provider) => new()
    {
        ["FileStorage:Provider"] = provider,
        ["FileStorage:S3ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
        ["FileStorage:S3Region"] = "auto",
        ["FileStorage:S3BucketName"] = "elmanhg-media",
        ["FileStorage:S3AccessKeyId"] = "not-a-secret-access-key",
        ["FileStorage:S3SecretAccessKey"] = "not-a-secret-secret-key",
    };

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings, bool withS3)
    {
        settings["FileStorage:LocalRootPath"] = "media";
        settings["FileStorage:PublicBaseUrl"] = "/api/media";
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddCoreFileStorage();
        if (withS3)
        {
            services.AddCoreS3FileStorage();
        }

        return services.BuildServiceProvider();
    }
}
