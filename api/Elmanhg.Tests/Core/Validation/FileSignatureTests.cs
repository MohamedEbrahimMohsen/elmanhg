using Core.Validation.Files;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Core.Validation;

public sealed class FileSignatureTests
{
    private static readonly FileSignature[] BuiltIns = [FileSignature.Png, FileSignature.Jpeg, FileSignature.WebP, FileSignature.Gif, FileSignature.WebM, FileSignature.Ogg, FileSignature.Mp4, FileSignature.Zip];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static TheoryData<string, byte[]> RealHeaders => new()
    {
        { "x.png", PngHeader },
        { "x.jpg", [0xFF, 0xD8, 0xFF, 0xE0] },
        { "x.jpeg", [0xFF, 0xD8, 0xFF, 0xDB] },
        { "x.webp", [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20] },
        { "x.gif", [0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x01, 0x00] },
        { "x.gif", [0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00] },
        { "x.webm", [0x1A, 0x45, 0xDF, 0xA3, 0x9F] },
        { "x.ogg", [0x4F, 0x67, 0x67, 0x53, 0x00, 0x02] },
        { "x.mp4", [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32] },
        { "x.m4a", [0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20] },
        { "x.zip", [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00] },
    };

    public static TheoryData<string, byte[]> OtherHeaders => new()
    {
        { "x.png", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46] },
        { "x.webp", [0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20] },
        { "x.gif", [0x47, 0x49, 0x46, 0x38, 0x38, 0x61, 0x01, 0x00] },
        { "x.mp4", [0x00, 0x00, 0x00, 0x18, 0x6D, 0x6F, 0x6F, 0x76, 0x6D, 0x70, 0x34, 0x32] },
    };

    [Theory]
    [MemberData(nameof(RealHeaders))]
    public void Matches_BuiltInSignature_ReturnsTrue(string fileName, byte[] content)
    {
        FileSignature.Matches(File(fileName, content), BuiltIns).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(OtherHeaders))]
    public void Matches_HeaderOfOtherFormat_ReturnsFalse(string fileName, byte[] content)
    {
        FileSignature.Matches(File(fileName, content), BuiltIns).Should().BeFalse();
    }

    [Fact]
    public void Matches_ExtensionNotListed_ReturnsFalse()
    {
        FileSignature.Matches(File("x.svg", PngHeader), [FileSignature.Png]).Should().BeFalse();
    }

    [Fact]
    public void Matches_TruncatedHeader_ReturnsFalse()
    {
        FileSignature.Matches(File("x.png", PngHeader[..4]), [FileSignature.Png]).Should().BeFalse();
    }

    [Fact]
    public void Matches_UppercaseExtension_ReturnsTrue()
    {
        FileSignature.Matches(File("X.PNG", PngHeader), [FileSignature.Png]).Should().BeTrue();
    }

    [Fact]
    public void ForExtensions_ZipAsXlsx_MatchesXlsxNotZip()
    {
        FileSignature[] signatures = [FileSignature.Zip.ForExtensions(".xlsx")];
        byte[] zipHeader = [0x50, 0x4B, 0x03, 0x04];

        FileSignature.Matches(File("q.xlsx", zipHeader), signatures).Should().BeTrue();
        FileSignature.Matches(File("q.zip", zipHeader), signatures).Should().BeFalse();
    }

    [Fact]
    public void Matches_ReadTwice_LeavesContentReadableFromStart()
    {
        var file = File("x.png", PngHeader);

        FileSignature.Matches(file, [FileSignature.Png]);

        using var stream = file.OpenReadStream();
        stream.ReadByte().Should().Be(0x89);
    }

    [Fact]
    public void Create_NoPatterns_ThrowsArgumentOutOfRange()
    {
        var act = () => FileSignature.Create([".bin"]);

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("patterns");
    }

    private static FormFile File(string fileName, byte[] content) => new(new MemoryStream(content), 0, content.Length, "file", fileName);
}
