using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.Shared;

public sealed class LessonContentMatchResultGeneratorTests
{
    private static readonly Guid QuestionId = Guid.Parse("3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f");

    [Theory]
    [InlineData(LessonContentSection.Explanation, "explanation-3")]
    [InlineData(LessonContentSection.Objectives, "objectives-3")]
    [InlineData(LessonContentSection.Summary, "summary-3")]
    [InlineData(LessonContentSection.QuestionExplanation, "question-3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f-3")]
    public void Generate_Section_FormatsReference(LessonContentSection section, string reference)
    {
        var match = new LessonContentMatch(Guid.NewGuid(), section, "Title", 3, section == LessonContentSection.QuestionExplanation ? QuestionId : null, "Content", 0.25);

        var result = LessonContentMatchResultGenerator.Generate(match);

        result.Reference.Should().Be(reference);
        result.Score.Should().Be(0.75);
        result.Should().BeEquivalentTo(new { match.ChunkId, match.Section, match.SectionTitle, match.Position, match.QuestionId, match.Content });
    }
}
