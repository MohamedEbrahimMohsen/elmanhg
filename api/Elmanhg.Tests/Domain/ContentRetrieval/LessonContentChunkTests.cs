using Core.Errors;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.SharedKernel.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.ContentRetrieval;

public sealed class LessonContentChunkTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidEmbedding_CopiesDraftAndVector()
    {
        var lessonId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var draft = new LessonContentChunkDraft(LessonContentSection.QuestionExplanation, "Title", 2, questionId, 3, "Content");
        var embedding = new float[LessonContentChunk.EmbeddingDimensions];
        embedding[7] = 0.5f;

        var chunk = LessonContentChunk.Create(lessonId, draft, embedding, "text-embedding-3-small", CreatedAt);

        chunk.Id.Should().NotBeEmpty();
        chunk.LessonId.Should().Be(lessonId);
        chunk.Section.Should().Be(LessonContentSection.QuestionExplanation);
        chunk.SectionTitle.Should().Be("Title");
        chunk.Position.Should().Be(2);
        chunk.QuestionId.Should().Be(questionId);
        chunk.QuestionVersion.Should().Be(3);
        chunk.Content.Should().Be("Content");
        chunk.Embedding.ToArray().Should().Equal(embedding);
        chunk.EmbeddingModel.Should().Be("text-embedding-3-small");
        chunk.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void Create_WrongDimensions_ThrowsContentEmbeddingDimensionsInvalid()
    {
        var draft = new LessonContentChunkDraft(LessonContentSection.Explanation, null, 1, null, null, "Content");

        var act = () => LessonContentChunk.Create(Guid.NewGuid(), draft, new float[LessonContentChunk.EmbeddingDimensions - 1], "fake", CreatedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ContentEmbeddingDimensionsInvalid);
    }
}
