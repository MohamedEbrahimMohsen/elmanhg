using Core.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Core.Storage;

public sealed class PublicMediaPipelineTests : IDisposable
{
    private static readonly byte[] Bytes = [0x89, 0x50, 0x4E, 0x47];
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "elmanhg-tests-pipeline", Guid.NewGuid().ToString("N"));
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();

    [Fact]
    public async Task UseCorePublicMedia_Local_ServesPublicFileWithImmutableCache()
    {
        WriteLocalFile("lessons", "a.png");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
        context.Response.Headers.CacheControl.ToString().Should().Be("public, max-age=31536000, immutable");
        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task UseCorePublicMedia_Local_PrivateFolder_Returns404()
    {
        WriteLocalFile("teacher-threads", "x.png");

        var context = await SendAsync(LocalOptions(), "/api/media/teacher-threads/x.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ((MemoryStream)context.Response.Body).Length.Should().Be(0);
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalSvgKey_ServesOctetStreamWithNosniff()
    {
        WriteLocalFile("lessons", "a.svg");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.svg");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/octet-stream");
        context.Response.Headers.XContentTypeOptions.ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalHtmlKey_ServesOctetStream()
    {
        WriteLocalFile("lessons", "a.html");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.html");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalWebmKey_ServesAudioWebm()
    {
        WriteLocalFile("lessons", "a.webm");

        var context = await SendAsync(LocalOptions(), "/api/media/lessons/a.webm");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("audio/webm");
    }

    [Fact]
    public async Task UseCorePublicMedia_LocalRootWithTrailingSlash_ServesPublicFile()
    {
        WriteLocalFile("lessons", "a.png");

        var context = await SendAsync(LocalOptions("media/"), "/api/media/lessons/a.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
    }

    [Fact]
    public async Task UseCorePublicMedia_S3_PrivateFolder_Returns404WithoutReading()
    {
        var context = await SendAsync(S3Options(), "/api/media/teacher-threads/x.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UseCorePublicMedia_S3_PublicKey_StreamsFromStorage()
    {
        _fileStorage.OpenReadAsync("lessons/a.png", Arg.Any<CancellationToken>()).Returns(new StoredFile(new MemoryStream(Bytes), Bytes.Length, "image/png"));

        var context = await SendAsync(S3Options(), "/api/media/lessons/a.png");

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        ((MemoryStream)context.Response.Body).ToArray().Should().Equal(Bytes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private static FileStorageOptions LocalOptions(string localRootPath = "media") => new() { Provider = FileStorageProvider.Local, LocalRootPath = localRootPath, PublicBaseUrl = "/api/media" };

    private static FileStorageOptions S3Options() => new() { Provider = FileStorageProvider.S3, LocalRootPath = "media", PublicBaseUrl = "/api/media", S3BucketName = "media", S3AccessKeyId = "not-a-secret-access-key", S3SecretAccessKey = "not-a-secret-secret-key" };

    private void WriteLocalFile(string folder, string name)
    {
        var directory = Path.Combine(_contentRoot, "media", folder);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), Bytes);
    }

    private async Task<HttpContext> SendAsync(FileStorageOptions options, string path)
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
