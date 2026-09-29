using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ContentRetrieval;

public sealed class LessonContentChunkDraftTests
{
    [Fact]
    public void EmbeddingText_WithTitle_PrefixesTitleLine()
    {
        var draft = new LessonContentChunkDraft(LessonContentSection.Explanation, "T", 1, null, null, "C");

        draft.EmbeddingText.Should().Be("T\nC");
    }

    [Fact]
    public void EmbeddingText_WithoutTitle_ReturnsContent()
    {
        var draft = new LessonContentChunkDraft(LessonContentSection.Explanation, null, 1, null, null, "C");

        draft.EmbeddingText.Should().Be("C");
    }
}
