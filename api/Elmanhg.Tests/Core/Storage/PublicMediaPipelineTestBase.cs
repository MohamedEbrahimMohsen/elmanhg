using Core.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Core.Storage;

public abstract class PublicMediaPipelineTestBase : IDisposable
{
    protected static readonly byte[] Bytes = [0x89, 0x50, 0x4E, 0x47];
    protected readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "elmanhg-tests-pipeline", Guid.NewGuid().ToString("N"));
    protected readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    protected static FileStorageOptions LocalOptions(string localRootPath = "media") => new() { Provider = FileStorageProvider.Local, LocalRootPath = localRootPath, PublicBaseUrl = "/api/media" };

    protected static FileStorageOptions S3Options() => new() { Provider = FileStorageProvider.S3, LocalRootPath = "media", PublicBaseUrl = "/api/media", S3BucketName = "media", S3AccessKeyId = "not-a-secret-access-key", S3SecretAccessKey = "not-a-secret-secret-key" };

    protected void WriteLocalFile(string folder, string name)
    {
        var directory = Path.Combine(_contentRoot, "media", folder);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), Bytes);
    }

    protected async Task<HttpContext> SendAsync(FileStorageOptions options, string path)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(_contentRoot);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton(Options.Create(options));
        services.AddScoped(_ => _fileStorage);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var app = new ApplicationBuilder(provider);
        app.UseCorePublicMedia(["teacher-threads"]);
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        await app.Build()(context);
        return context;
    }
}
