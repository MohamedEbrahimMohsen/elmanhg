using Elmanhg.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.Storage;

public sealed class LocalDiskFileStorageTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "elmanhg-tests-storage", Guid.NewGuid().ToString("N"));
    private readonly LocalDiskFileStorage _storage;

    public LocalDiskFileStorageTests()
    {
        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.ContentRootPath.Returns(_contentRoot);
        _storage = new LocalDiskFileStorage(Options.Create(new FileStorageOptions { Provider = FileStorageProvider.Local, LocalRootPath = "media", PublicBaseUrl = "/api/media" }), hostEnvironment);
    }

    [Fact]
    public async Task SaveAsync_ValidKey_WritesFileAndReturnsPublicUrl()
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47];

        var url = await _storage.SaveAsync(new MemoryStream(bytes), "lessons/a/b.png", TestContext.Current.CancellationToken);

        url.Should().Be("/api/media/lessons/a/b.png");
        (await File.ReadAllBytesAsync(Path.Combine(_contentRoot, "media", "lessons", "a", "b.png"), TestContext.Current.CancellationToken)).Should().Equal(bytes);
    }

    [Fact]
    public async Task SaveAsync_KeyEscapingRoot_ThrowsArgumentException()
    {
        var act = () => _storage.SaveAsync(new MemoryStream([1]), "../escape.png", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
        File.Exists(Path.Combine(_contentRoot, "escape.png")).Should().BeFalse();
    }

    [Fact]
    public async Task OpenReadAsync_ExistingKey_ReturnsContentLengthAndType()
    {
        byte[] bytes = [0x1A, 0x45, 0xDF, 0xA3, 0x01];
        await _storage.SaveAsync(new MemoryStream(bytes), "teacher-threads/a.webm", TestContext.Current.CancellationToken);

        await using var file = await _storage.OpenReadAsync("teacher-threads/a.webm", TestContext.Current.CancellationToken);

        using var copy = new MemoryStream();
        await file!.Content.CopyToAsync(copy, TestContext.Current.CancellationToken);
        copy.ToArray().Should().Equal(bytes);
        (file.Length, file.ContentType).Should().Be((5L, "audio/webm"));
    }

    [Fact]
    public async Task OpenReadAsync_MissingKey_ReturnsNull()
    {
        var file = await _storage.OpenReadAsync("teacher-threads/missing.webm", TestContext.Current.CancellationToken);

        file.Should().BeNull();
    }

    [Fact]
    public async Task OpenReadAsync_KeyEscapingRoot_ThrowsArgumentException()
    {
        var act = () => _storage.OpenReadAsync("../escape.webm", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }
}
