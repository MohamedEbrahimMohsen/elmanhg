using Elmanhg.Application.TeacherThreads.CreateTeacherThread;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.CreateTeacherThread;

public sealed class TeacherThreadImageFormatsTests
{
    public static TheoryData<string, byte[]> RealImages => new()
    {
        { "photo.png", TeacherThreadImageSignatures.Png },
        { "photo.PNG", TeacherThreadImageSignatures.Png },
        { "photo.jpg", TeacherThreadImageSignatures.Jpeg },
        { "photo.jpeg", TeacherThreadImageSignatures.Jpeg },
        { "photo.webp", TeacherThreadImageSignatures.Webp },
    };

    public static TheoryData<string, byte[]> SpoofedImages => new()
    {
        { "photo.png", "<svg onload=alert(1)>"u8.ToArray() },
        { "photo.png", TeacherThreadImageSignatures.Jpeg },
        { "photo.jpg", TeacherThreadImageSignatures.Png },
        { "photo.webp", [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVE"u8] },
        { "photo.png", [0x89, 0x50, 0x4E, 0x47] },
        { "photo.png", [] },
        { "photo.gif", "GIF89a"u8.ToArray() },
    };

    [Theory]
    [MemberData(nameof(RealImages))]
    public void HasMatchingSignature_RealImage_ReturnsTrue(string fileName, byte[] bytes)
    {
        TeacherThreadImageFormats.HasMatchingSignature(File(fileName, bytes)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SpoofedImages))]
    public void HasMatchingSignature_SpoofedImage_ReturnsFalse(string fileName, byte[] bytes)
    {
        TeacherThreadImageFormats.HasMatchingSignature(File(fileName, bytes)).Should().BeFalse();
    }

    [Fact]
    public void HasMatchingSignature_ReadTwice_LeavesContentReadableFromStart()
    {
        var file = File("photo.png", TeacherThreadImageSignatures.Png);

        TeacherThreadImageFormats.HasMatchingSignature(file);
        using var stream = file.OpenReadStream();
        var first = stream.ReadByte();

        first.Should().Be(0x89);
    }

    private static FormFile File(string fileName, byte[] bytes) => new(new MemoryStream(bytes), 0, bytes.Length, "image", fileName) { Headers = new HeaderDictionary(), ContentType = "image/png" };
}
