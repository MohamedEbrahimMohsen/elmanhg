using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Avatar.Shared;

public sealed class AvatarContextBundleFactoryTests
{
    private readonly QuestionBuilder _content = new();
    private readonly RichTextExtractor _extractor = new();

    [Fact]
    public void ForLesson_WithSources_OmitsExplanationAndSummary()
    {
        var lesson = BuildLesson("<p>V = I R</p>", "<p>R = V / I</p>");

        var bundle = AvatarContextBundleFactory.ForLesson(AvatarEntryPoint.Lesson, _content.Subject, _content.Unit, lesson, null, true, _extractor, 8000);

        bundle.EntryPoint.Should().Be(AiChatEntryPoint.Lesson);
        bundle.Subject.Should().Be(new AiContextReference(_content.Subject.Id, _content.Subject.Name));
        bundle.Unit.Should().Be(new AiContextReference(_content.Unit.Id, _content.Unit.Name));
        bundle.Lesson!.Explanation.Should().BeEmpty();
        bundle.Lesson.Summary.Should().BeEmpty();
        bundle.Lesson.Objectives.Should().Equal("الهدف الأول", "الهدف الثاني");
        bundle.Subjects.Should().BeEmpty();
    }

    [Fact]
    public void ForLesson_WithoutSources_SendsPlainTextTruncated()
    {
        var lesson = BuildLesson($"<p>{new string('a', 600)}</p>", "<p>R = V / I</p>");

        var bundle = AvatarContextBundleFactory.ForLesson(AvatarEntryPoint.QuizQuestion, _content.Subject, _content.Unit, lesson, null, false, _extractor, 500);

        bundle.EntryPoint.Should().Be(AiChatEntryPoint.QuizQuestion);
        bundle.Lesson!.Explanation.Should().Be(new string('a', 500));
        bundle.Lesson.Summary.Should().Be("R = V / I");
    }

    [Fact]
    public void ForGlobal_SendsSubjectsOnly()
    {
        var bundle = AvatarContextBundleFactory.ForGlobal(["الفيزياء", "الأحياء"]);

        bundle.EntryPoint.Should().Be(AiChatEntryPoint.Global);
        bundle.Lesson.Should().BeNull();
        bundle.Subject.Should().BeNull();
        bundle.Question.Should().BeNull();
        bundle.Subjects.Should().Equal("الفيزياء", "الأحياء");
    }

    private Lesson BuildLesson(string explanation, string summary)
    {
        var lesson = Lesson.Create(_content.Unit, "قانون أوم", 2, Guid.NewGuid());
        lesson.Update(lesson.Name, explanation, summary, null, [new LessonObjectiveContent(null, "الهدف الأول"), new LessonObjectiveContent(null, "الهدف الثاني")], Guid.NewGuid());
        return lesson;
    }
}
