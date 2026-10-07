using Core.Storage.Media;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;

namespace Elmanhg.Tests.Core.Storage;

public sealed class PublicMediaFileProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "elmanhg-tests-media", Guid.NewGuid().ToString("N"));
    private readonly PhysicalFileProvider _files;
    private readonly PublicMediaFileProvider _provider;

    public PublicMediaFileProviderTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "lessons"));
        Directory.CreateDirectory(Path.Combine(_root, "teacher-threads"));
        File.WriteAllBytes(Path.Combine(_root, "lessons", "a.png"), [1]);
        File.WriteAllBytes(Path.Combine(_root, "teacher-threads", "x.png"), [1]);
        _files = new PhysicalFileProvider(_root);
        _provider = new PublicMediaFileProvider(_files, ["teacher-threads", "training-exports"]);
    }

    [Fact]
    public void GetFileInfo_PublicFile_ReturnsExistingFile()
    {
        _provider.GetFileInfo("/lessons/a.png").Exists.Should().BeTrue();
    }

    [Theory]
    [InlineData("/teacher-threads/x.png")]
    [InlineData("/TEACHER-THREADS/x.png")]
    public void GetFileInfo_PrivateFolder_ReturnsNotFound(string subpath)
    {
        _provider.GetFileInfo(subpath).Exists.Should().BeFalse();
    }

    [Fact]
    public void GetFileInfo_UpperCasePrivateFolderWithExistingFile_ReturnsNotFound()
    {
        Directory.CreateDirectory(Path.Combine(_root, "TEACHER-THREADS"));
        File.WriteAllBytes(Path.Combine(_root, "TEACHER-THREADS", "upper.png"), [1]);

        var stored = _files.GetFileInfo("/TEACHER-THREADS/upper.png");
        var served = _provider.GetFileInfo("/TEACHER-THREADS/upper.png");

        stored.Exists.Should().BeTrue();
        served.Exists.Should().BeFalse();
    }

    [Fact]
    public void GetFileInfo_ShortNameAlias_ReturnsNotFound()
    {
        _provider.GetFileInfo("/TEACHE~1/x.png").Exists.Should().BeFalse();
    }

    [Fact]
    public void GetDirectoryContents_AnyPath_ReturnsNotFound()
    {
        _provider.GetDirectoryContents("/lessons").Exists.Should().BeFalse();
    }

    public void Dispose()
    {
        _files.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
