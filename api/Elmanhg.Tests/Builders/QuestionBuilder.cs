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
    private bool _dragDrop;
    private bool _mathSteps;
    private bool _mathStepsGraded;

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

    public QuestionBuilder DragDrop()
    {
        _dragDrop = true;
        return this;
    }

    public QuestionBuilder MathSteps()
    {
        _mathSteps = true;
        return this;
    }

    public QuestionBuilder MathStepsGraded()
    {
        _mathSteps = true;
        _mathStepsGraded = true;
        return this;
    }

    public Question Build()
    {
        var type = _dragDrop ? QuestionType.DragDrop : _mathSteps ? QuestionType.MathSteps : _essay ? QuestionType.Essay : QuestionType.Mcq;
        var content = _dragDrop ? DragDropContent() : _mathStepsGraded ? MathStepsContent(MathStepsGradedSpecJson) : _mathSteps ? MathStepsContent() : _essay ? EssayContent() : McqContent();
        var question = Question.Create(Lesson, Unit, type, content, _metadata ?? new QuestionMetadata(QuestionDifficulty.Medium, null, []), Guid.NewGuid());
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

    public const string DragDropImageLessonId = "0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70";

    public const string DragDropImageKey = $"question-diagrams/{DragDropImageLessonId}/0123456789abcdef0123456789abcdef.png";

    public const string DragDropBodyJson = $$"""{"image":{"key":"{{DragDropImageKey}}","width":800,"height":600,"alt":"Plant cell"},"zones":[{"id":"z1","x":10,"y":10,"width":20,"height":15,"capacity":2},{"id":"z2","x":50,"y":40,"width":30,"height":20.5,"capacity":2}],"items":[{"id":"i1","text":"Nucleus"},{"id":"i2","text":"Vacuole"},{"id":"i3","text":"Wall"},{"id":"i4","text":"Membrane"},{"id":"i5","text":"Engine"}]}""";

    public const string DragDropSpecJson = """{"zones":[{"zoneId":"z1","itemIds":["i1","i2"],"ordered":false},{"zoneId":"z2","itemIds":["i4","i3"],"ordered":true}]}""";

    public static QuestionContent DragDropContent() => new("<p>Label the plant cell.</p>", DragDropBodyJson, DragDropSpecJson, "<p>Parts of a cell.</p>", 4);

    public static string ForLesson(string json, Guid lessonId) => json.Replace(DragDropImageLessonId, lessonId.ToString(), StringComparison.Ordinal);

    public static QuestionFields DragDropFields() => new(QuestionType.DragDrop, "<p>Label the plant cell.</p>", Json(DragDropBodyJson), Json(DragDropSpecJson), "<p>Parts of a cell.</p>", QuestionDifficulty.Medium, null, [], 4);

    public const string MathStepsSpecJson = """{"acceptedAnswers":["x = 2"],"form":"equivalent"}""";
    public const string MathStepsGradedSpecJson = """{"acceptedAnswers":["x = 2"],"form":"equivalent","modelSolution":["2x = 4","x = 2"],"stepsWeight":50}""";

    public static QuestionContent MathStepsContent(string gradingSpec = MathStepsSpecJson)
    {
        return new QuestionContent("<p>Solve 2x + 3 = 7.</p>", "{}", gradingSpec, "<p>Subtract 3, divide by 2.</p>", 2);
    }

    public static QuestionFields MathStepsFields()
    {
        return new QuestionFields(QuestionType.MathSteps, "<p>Solve 2x + 3 = 7.</p>", Json("{}"), Json(MathStepsSpecJson), "<p>Subtract 3, divide by 2.</p>", QuestionDifficulty.Medium, null, [], 2);
    }

    public static QuestionFields MathStepsGradedFields()
    {
        return new QuestionFields(QuestionType.MathSteps, "<p>Solve 2x + 3 = 7.</p>", Json("{}"), Json(MathStepsGradedSpecJson), "<p>Subtract 3, divide by 2.</p>", QuestionDifficulty.Medium, null, [], 2);
    }

    public static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
