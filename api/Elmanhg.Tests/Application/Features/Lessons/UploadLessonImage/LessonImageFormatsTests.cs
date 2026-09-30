using Elmanhg.Application.Lessons.UploadLessonImage;
using Elmanhg.Tests.Application.Features.TeacherThreads.CreateTeacherThread;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Application.Features.Lessons.UploadLessonImage;

public sealed class LessonImageFormatsTests
{
    public static TheoryData<string, byte[]> RealImages => new()
    {
        { "x.png", TeacherThreadImageSignatures.Png },
        { "x.PNG", TeacherThreadImageSignatures.Png },
        { "x.jpg", TeacherThreadImageSignatures.Jpeg },
        { "x.jpeg", TeacherThreadImageSignatures.Jpeg },
        { "x.webp", TeacherThreadImageSignatures.Webp },
        { "x.gif", [.. "GIF89a"u8, 0, 0, 0, 0, 0, 0] },
        { "x.gif", [.. "GIF87a"u8, 0, 0, 0, 0, 0, 0] },
    };

    public static TheoryData<string, byte[]> SpoofedImages => new()
    {
        { "x.png", "<html><script>"u8.ToArray() },
        { "x.png", TeacherThreadImageSignatures.Jpeg },
        { "x.gif", TeacherThreadImageSignatures.Png },
        { "x.gif", [.. "GIF88a"u8, 0, 0, 0, 0, 0, 0] },
        { "x.webp", [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVE"u8] },
        { "x.png", TeacherThreadImageSignatures.Png[..4] },
        { "x.png", [] },
    };

    [Theory]
    [MemberData(nameof(RealImages))]
    public void HasMatchingSignature_RealImage_ReturnsTrue(string fileName, byte[] content)
    {
        LessonImageFormats.HasMatchingSignature(File(fileName, content)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SpoofedImages))]
    public void HasMatchingSignature_SpoofedImage_ReturnsFalse(string fileName, byte[] content)
    {
        LessonImageFormats.HasMatchingSignature(File(fileName, content)).Should().BeFalse();
    }

    private static FormFile File(string fileName, byte[] content) => new(new MemoryStream(content), 0, content.Length, "file", fileName);
}
