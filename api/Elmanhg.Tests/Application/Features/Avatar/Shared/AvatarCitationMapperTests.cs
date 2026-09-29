using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.Shared;

public sealed class AvatarCitationMapperTests
{
    private static readonly Guid LessonId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();

    [Fact]
    public void Map_KnownCitations_ReturnsThemInCitationOrderDistinct()
    {
        var matches = new[] { AvatarTestData.Match("explanation-1", LessonContentSection.Explanation, "قانون أوم"), AvatarTestData.Match("question-1", LessonContentSection.QuestionExplanation, questionId: QuestionId) };

        var citations = AvatarCitationMapper.Map(["question-1", "explanation-1", "question-1"], matches, LessonId);

        citations.Should().Equal(
            new AvatarCitationResult("question-1", LessonContentSection.QuestionExplanation, null, LessonId, QuestionId),
            new AvatarCitationResult("explanation-1", LessonContentSection.Explanation, "قانون أوم", LessonId, null));
    }

    [Fact]
    public void Map_UnknownReference_IsDropped()
    {
        var citations = AvatarCitationMapper.Map(["summary-9", "explanation-1"], [AvatarTestData.Match("explanation-1", LessonContentSection.Explanation)], LessonId);

        citations.Select(x => x.Reference).Should().Equal("explanation-1");
    }

    [Fact]
    public void Map_NoLesson_ReturnsEmpty()
    {
        var citations = AvatarCitationMapper.Map(["explanation-1"], [AvatarTestData.Match("explanation-1", LessonContentSection.Explanation)], null);

        citations.Should().BeEmpty();
    }
}
