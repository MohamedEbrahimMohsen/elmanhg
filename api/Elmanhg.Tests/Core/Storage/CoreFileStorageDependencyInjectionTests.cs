using Core.Storage;
using Core.Storage.Local;
using Core.Storage.S3;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Core.Storage;

public sealed class CoreFileStorageDependencyInjectionTests
{
    [Fact]
    public void AddCoreFileStorage_Local_ResolvesLocalDiskFileStorage()
    {
        using var provider = BuildProvider(new() { ["FileStorage:Provider"] = "Local" });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFileStorage>().Should().BeOfType<LocalDiskFileStorage>();
    }

    [Fact]
    public void AddCoreFileStorage_S3_ResolvesS3FileStorage()
    {
        using var provider = BuildProvider(new()
        {
            ["FileStorage:Provider"] = "S3",
            ["FileStorage:S3ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
            ["FileStorage:S3Region"] = "auto",
            ["FileStorage:S3BucketName"] = "elmanhg-media",
            ["FileStorage:S3AccessKeyId"] = "not-a-secret-access-key",
            ["FileStorage:S3SecretAccessKey"] = "not-a-secret-secret-key",
        });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFileStorage>().Should().BeOfType<S3FileStorage>();
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        settings["FileStorage:LocalRootPath"] = "media";
        settings["FileStorage:PublicBaseUrl"] = "/api/media";
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddCoreFileStorage();
        return services.BuildServiceProvider();
    }
}
