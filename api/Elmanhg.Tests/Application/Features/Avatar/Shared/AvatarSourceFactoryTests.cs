using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.Shared;

public sealed class AvatarSourceFactoryTests
{
    [Fact]
    public void Create_ExplanationWithTitle_TitleIsLabelAndSectionTitle()
    {
        var source = AvatarSourceFactory.Create(AvatarTestData.Match("explanation-1", LessonContentSection.Explanation, "قانون أوم"));

        source.Reference.Should().Be("explanation-1");
        source.Title.Should().Be("الشرح — قانون أوم");
        source.Content.Should().Be("content explanation-1");
    }

    [Fact]
    public void Create_SummaryWithoutTitle_TitleIsLabelOnly()
    {
        var source = AvatarSourceFactory.Create(AvatarTestData.Match("summary-1", LessonContentSection.Summary));

        source.Title.Should().Be("الملخص");
    }

    [Fact]
    public void Create_QuestionExplanation_UsesQuestionLabel()
    {
        var source = AvatarSourceFactory.Create(AvatarTestData.Match("question-1", LessonContentSection.QuestionExplanation, questionId: Guid.NewGuid()));

        source.Title.Should().Be("شرح سؤال");
    }
}
