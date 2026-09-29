using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.Shared;

public sealed class AvatarMessageJsonTests
{
    private static readonly AiContextBundle Bundle = new(
        AiChatEntryPoint.QuizQuestion,
        new AiContextReference(Guid.NewGuid(), "الفيزياء"),
        new AiContextReference(Guid.NewGuid(), "الكهرباء"),
        new AiLessonContext(Guid.NewGuid(), "قانون أوم", "V = I R", ["يحسب المقاومة"], "R = V / I"),
        new AiQuestionContext(Guid.NewGuid(), "ما المقاومة؟", "2", "3", null),
        []);

    private static readonly List<AiChatSource> Sources = [new("explanation-1", "الشرح — قانون أوم", "V = I R")];

    [Fact]
    public void WriteContext_ThenReadContext_RoundTripsBundleAndSources()
    {
        var context = AvatarMessageJson.ReadContext(AvatarMessageJson.WriteContext(Bundle, Sources));

        context.Bundle.Should().BeEquivalentTo(Bundle);
        context.Sources.Should().Equal(Sources);
    }

    [Fact]
    public void WriteContext_EntryPoint_IsCamelCase()
    {
        var json = AvatarMessageJson.WriteContext(Bundle, Sources);

        json.Should().Contain("\"entryPoint\":\"quizQuestion\"").And.Contain("\"sources\"");
    }

    [Fact]
    public void WriteCitations_ThenReadCitations_RoundTrips()
    {
        List<AvatarCitationResult> citations = [new("explanation-1", LessonContentSection.Explanation, "قانون أوم", Guid.NewGuid(), null), new("question-1", LessonContentSection.QuestionExplanation, null, Guid.NewGuid(), Guid.NewGuid())];

        var read = AvatarMessageJson.ReadCitations(AvatarMessageJson.WriteCitations(citations));

        read.Should().Equal(citations);
    }
}
