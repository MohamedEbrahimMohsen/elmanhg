using Elmanhg.Application.Questions.Shared;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class DiagramImageKeyTests
{
    private const string Folder = "question-diagrams/0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70";
    private static readonly Guid LessonId = Guid.Parse("0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70");
    private const string Name = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData($"{Folder}/{Name}.png")]
    [InlineData($"{Folder}/{Name}.jpg")]
    [InlineData($"{Folder}/{Name}.jpeg")]
    [InlineData($"{Folder}/{Name}.webp")]
    public void IsValid_StoredDiagramKey_ReturnsTrue(string key)
    {
        DiagramImageKey.IsValid(key).Should().BeTrue();
    }

    [Fact]
    public void BelongsToLesson_KeyInLessonFolder_ReturnsTrue()
    {
        DiagramImageKey.BelongsToLesson($"{Folder}/{Name}.png", LessonId).Should().BeTrue();
    }

    [Theory]
    [InlineData($"question-diagrams/1b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70/{Name}.png")]
    [InlineData($"{Folder}/../{Name}.png")]
    [InlineData(null)]
    public void BelongsToLesson_OtherLessonOrInvalidKey_ReturnsFalse(string? key)
    {
        DiagramImageKey.BelongsToLesson(key, LessonId).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData($"/api/media/{Folder}/{Name}.png")]
    [InlineData($"https://cdn.example.com/media/{Folder}/{Name}.png")]
    [InlineData($"lessons/0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70/{Name}.png")]
    [InlineData($"{Folder}/{Name}.svg")]
    [InlineData($"{Folder}/{Name}.PNG")]
    [InlineData($"{Folder}/{Name}.png?x=1")]
    [InlineData($"{Folder}/../{Name}.png")]
    [InlineData($"javascript:alert(1)//{Folder}/{Name}.png")]
    [InlineData("data:image/png;base64,AAAA")]
    public void IsValid_OtherKey_ReturnsFalse(string? key)
    {
        DiagramImageKey.IsValid(key).Should().BeFalse();
    }
}
