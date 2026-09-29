using Elmanhg.Application.Questions.Shared;
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
    private string? _rejectionReason;
    private bool _retired;
    private bool _essay;

    public QuestionBuilder()
    {
        Unit = CurriculumUnit.Create(Subject, "Mechanics", 1, Guid.NewGuid());
        Lesson = Lesson.Create(Unit, "Newton's laws", 1, Guid.NewGuid());
        Lesson.Update("Newton's laws", string.Empty, string.Empty, null, [new LessonObjectiveContent(null, "State the first law")], Guid.NewGuid());
    }

    public Subject Subject { get; } = Subject.Create("Physics", 1, Guid.NewGuid());

    public CurriculumUnit Unit { get; }

    public Lesson Lesson { get; }

    public User Teacher { get; } = User.CreateTeacher("Teacher", "teacher@example.com");

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

    public QuestionBuilder Rejected(string reason)
    {
        _rejectionReason = reason;
        return this;
    }

    public QuestionBuilder Retired()
    {
        _retired = true;
        return this;
    }

    public QuestionBuilder Essay()
    {
        _essay = true;
        return this;
    }

    public Question Build()
    {
        var question = Question.Create(Lesson, Unit, _essay ? QuestionType.Essay : QuestionType.Mcq, _essay ? EssayContent() : McqContent(), _metadata ?? new QuestionMetadata(QuestionDifficulty.Medium, null, []), Guid.NewGuid());
        if (_approved)
        {
            question.Approve(TeacherSubject.Create(Teacher, Subject, Guid.NewGuid()), question.Version);
        }

        if (_rejectionReason is not null)
        {
            question.Reject(TeacherSubject.Create(Teacher, Subject, Guid.NewGuid()), question.Version, _rejectionReason);
        }

        if (_retired)
        {
            question.Retire(Guid.NewGuid());
        }

        return question;
    }

    public static QuestionContent McqContent()
    {
        return new QuestionContent("<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", "<p>Add the numbers.</p>", 1);
    }

    public static QuestionFields McqFields(string stem = "<p>2 + 2 = ?</p>")
    {
        return new QuestionFields(QuestionType.Mcq, stem, Json("""{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}"""), Json("""{"correctOptionId":"b"}"""), "<p>Add the numbers.</p>", QuestionDifficulty.Medium, null, [], 1);
    }

    public const string EssaySpecJson = """{"criteria":[{"id":"c1","title":"Definition","points":2,"levels":[{"points":0,"description":"Missing"},{"points":1,"description":"Partial"},{"points":2,"description":"Complete"}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"]}""";

    public static QuestionContent EssayContent()
    {
        return new QuestionContent("<p>Explain inertia.</p>", """{"maxWords":200}""", EssaySpecJson, "<p>Newton 1.</p>", 5);
    }

    public static QuestionFields EssayFields()
    {
        return new QuestionFields(QuestionType.Essay, "<p>Explain inertia.</p>", Json("""{"maxWords":200}"""), Json(EssaySpecJson), "<p>Newton 1.</p>", QuestionDifficulty.Medium, null, [], 5);
    }

    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
