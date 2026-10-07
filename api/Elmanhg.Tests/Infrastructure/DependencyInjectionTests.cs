using Core.Http;
using Core.Storage;
using Core.Storage.S3;
using Elmanhg.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_NoUserAgentConfigured_SendsElmanhgProductToken()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CoreHttpOptions>>().Value.UserAgent.Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public void AddInfrastructure_S3Provider_ResolvesS3FileStorage()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileStorage:Provider"] = "S3",
            ["FileStorage:S3ServiceUrl"] = "https://account.r2.cloudflarestorage.com",
            ["FileStorage:S3Region"] = "auto",
            ["FileStorage:S3BucketName"] = "elmanhg-media",
            ["FileStorage:S3AccessKeyId"] = "not-a-secret-access-key",
            ["FileStorage:S3SecretAccessKey"] = "not-a-secret-secret-key",
            ["FileStorage:LocalRootPath"] = "media",
            ["FileStorage:PublicBaseUrl"] = "/api/media",
        }).Build());
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFileStorage>().Should().BeOfType<S3FileStorage>();
    }
}
