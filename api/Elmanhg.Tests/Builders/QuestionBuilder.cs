using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using System.Text.Json;

namespace Elmanhg.Tests.Builders;

public sealed class QuestionBuilder
{
    private QuestionMetadata? _metadata;
    private bool _approved;

    public QuestionBuilder()
    {
        Unit = CurriculumUnit.Create(Subject, "Mechanics", 1, Guid.NewGuid());
        Lesson = Lesson.Create(Unit, "Newton's laws", 1, Guid.NewGuid());
        Lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "State the first law")], Guid.NewGuid());
    }

    public Subject Subject { get; } = Subject.Create("Physics", 1, Guid.NewGuid());

    public CurriculumUnit Unit { get; }

    public Lesson Lesson { get; }

    public Guid ObjectiveId => Lesson.Objectives[0].Id;

    public QuestionBuilder WithMetadata(QuestionMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public QuestionBuilder Approved()
    {
        _approved = true;
        return this;
    }

    public Question Build()
    {
        var question = Question.Create(Lesson, Unit, QuestionType.Mcq, McqContent(), _metadata ?? new QuestionMetadata(QuestionDifficulty.Medium, null, []), Guid.NewGuid());
        if (_approved)
        {
            question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), Subject, Guid.NewGuid()));
        }

        return question;
    }

    public static QuestionContent McqContent()
    {
        return new QuestionContent("<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", "<p>Add the numbers.</p>", 1);
    }

    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
