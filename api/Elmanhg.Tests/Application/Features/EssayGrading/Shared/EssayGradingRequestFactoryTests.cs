using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.RichText;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.EssayGrading.Shared;

public sealed class EssayGradingRequestFactoryTests
{
    private const string Spec = """{"criteria":[{"id":"c2","title":"Example","points":3,"levels":[{"points":0,"description":"None"},{"points":3,"description":"Clear example"}]},{"id":"c1","title":"Definition","description":"States the law","points":2,"levels":[{"points":0,"description":"Missing"},{"points":2,"description":"Complete"}]}],"modelAnswers":["<p>Inertia is <strong>resistance</strong>.</p>","<p><img src=\"/api/media/a.png\" alt=\"\"></p>"]}""";
    private readonly RichTextExtractor _extractor = new();

    [Fact]
    public void Create_BuildsPlainTextQuestionRubricAndModelAnswers()
    {
        var request = EssayGradingRequestFactory.Create("<p>Explain <em>inertia</em>.</p>", Spec, "essay", null, _extractor, 20000);

        request.Question.Should().Be("Explain inertia.");
        request.Criteria.Should().BeEquivalentTo(
            [
                new AiRubricCriterion("c2", "Example", null, 3, [new AiRubricLevel(0, "None"), new AiRubricLevel(3, "Clear example")]),
                new AiRubricCriterion("c1", "Definition", "States the law", 2, [new AiRubricLevel(0, "Missing"), new AiRubricLevel(2, "Complete")]),
            ],
            options => options.WithStrictOrdering());
        request.ModelAnswers.Should().Equal("Inertia is resistance.");
    }

    [Fact]
    public void Create_DropsModelAnswersWithoutText()
    {
        var request = EssayGradingRequestFactory.Create("<p>Q</p>", Spec, "essay", null, _extractor, 20000);

        request.ModelAnswers.Should().HaveCount(1);
    }

    [Fact]
    public void Create_TruncatesFieldsToMaxLength()
    {
        var request = EssayGradingRequestFactory.Create("<p>Q</p>", Spec, "essay", new EssayGradingContext("Physics", ["Objective one"]), _extractor, 5);

        (request.Criteria[1].Title, request.Objectives[0]).Should().Be(("Defin", "Objec"));
    }

    [Fact]
    public void Create_NoContext_LeavesSubjectNullAndObjectivesEmpty()
    {
        var request = EssayGradingRequestFactory.Create("<p>Q</p>", Spec, "  essay  ", null, _extractor, 20000);

        (request.Subject, request.Essay).Should().Be(((string?)null, "essay"));
        request.Objectives.Should().BeEmpty();
    }

    [Fact]
    public void Create_NullSpec_ThrowsInvalidOperationException()
    {
        var act = () => EssayGradingRequestFactory.Create("<p>Q</p>", "null", "essay", null, _extractor, 20000);

        act.Should().Throw<InvalidOperationException>();
    }
}
