using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.Shared;

public sealed class LessonContentChunkPlannerTests
{
    private const int Max = 1500;

    private readonly RichTextExtractor _extractor = new();
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void Plan_FullLesson_EmitsSectionsInOrder()
    {
        var lesson = BuildLesson("<h2>Law</h2><p>Current</p><h2>Power</h2><p>Watts</p>", "<p>Recap</p>", ["a"]);
        var question = _builder.Approved().Build();

        var drafts = LessonContentChunkPlanner.Plan(lesson, [question], _extractor, Max);

        drafts.Select(x => x.Section).Should().Equal(LessonContentSection.Explanation, LessonContentSection.Explanation, LessonContentSection.Objectives, LessonContentSection.Summary, LessonContentSection.QuestionExplanation);
        drafts.Take(2).Select(x => x.SectionTitle).Should().Equal("Law", "Power");
    }

    [Fact]
    public void Plan_Objectives_NumberedInOrder()
    {
        var lesson = BuildLesson(string.Empty, string.Empty, ["a", "b"]);

        var drafts = LessonContentChunkPlanner.Plan(lesson, [], _extractor, Max);

        var objectives = drafts.Should().ContainSingle().Which;
        objectives.Section.Should().Be(LessonContentSection.Objectives);
        objectives.Content.Should().Be("1. a\n2. b");
    }

    [Fact]
    public void Plan_Question_CombinesStemAndExplanationWithVersion()
    {
        var lesson = BuildLesson(string.Empty, string.Empty, []);
        var question = _builder.Approved().Build();

        var drafts = LessonContentChunkPlanner.Plan(lesson, [question], _extractor, Max);

        var chunk = drafts.Should().ContainSingle().Which;
        chunk.Section.Should().Be(LessonContentSection.QuestionExplanation);
        chunk.QuestionId.Should().Be(question.Id);
        chunk.QuestionVersion.Should().Be(question.Version);
        chunk.Content.Should().Be("2 + 2 = ?\nAdd the numbers.");
    }

    [Fact]
    public void Plan_EmptyLessonNoQuestions_ReturnsEmpty()
    {
        var lesson = BuildLesson(string.Empty, string.Empty, []);

        var drafts = LessonContentChunkPlanner.Plan(lesson, [], _extractor, Max);

        drafts.Should().BeEmpty();
    }

    private Lesson BuildLesson(string explanation, string summary, IReadOnlyList<string> objectives)
    {
        var lesson = Lesson.Create(_builder.Unit, "Ohm", 2, Guid.NewGuid());
        lesson.Update("Ohm", explanation, summary, null, objectives.Select(x => new LessonObjectiveContent(null, x)).ToList(), Guid.NewGuid());
        return lesson;
    }
}
