using Elmanhg.Application.Questions.Shared.Import;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Import;

public sealed class QuestionImportFileTests
{
    public static TheoryData<byte[]> NotZip => new()
    {
        "stem,correct"u8.ToArray(),
        new byte[] { 0x50, 0x4B },
        Array.Empty<byte>(),
    };

    [Fact]
    public void HasZipSignature_ZipHeader_ReturnsTrue()
    {
        byte[] content = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00];

        QuestionImportFile.HasZipSignature(File(content)).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NotZip))]
    public void HasZipSignature_NotZip_ReturnsFalse(byte[] content)
    {
        QuestionImportFile.HasZipSignature(File(content)).Should().BeFalse();
    }

    private static FormFile File(byte[] content) => new(new MemoryStream(content), 0, content.Length, "file", "q.xlsx");
}
