using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;

namespace Elmanhg.Tests.Builders;

public sealed class MultiUnitExamBuilder
{
    public const int MaxTimeLimitMinutes = 300;

    public static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    public MultiUnitExamBuilder()
    {
        Units = [CurriculumUnit.Create(Subject, "Mechanics", 1, Guid.NewGuid()), CurriculumUnit.Create(Subject, "Waves", 2, Guid.NewGuid())];
        Lessons = Units
            .Select(unit => Lesson.Create(unit, $"{unit.Name} basics", 1, Guid.NewGuid()))
            .ToList();
        Lessons.ForEach(x => x.Publish(Guid.NewGuid()));
    }

    public Subject Subject { get; } = Subject.Create("Physics", 1, Guid.NewGuid());

    public List<CurriculumUnit> Units { get; }

    public List<Lesson> Lessons { get; }

    public User Teacher { get; } = User.CreateTeacher("Teacher", "teacher@example.com");

    public Guid StudentId { get; } = Guid.NewGuid();

    public Question Approved(int unitIndex)
    {
        var question = Question.Create(Lessons[unitIndex], Units[unitIndex], QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), Guid.NewGuid());
        question.Approve(TeacherSubject.Create(Teacher, Subject, Guid.NewGuid()), question.Version);
        return question;
    }

    public ExamBlueprint UnitBlueprint(int unitIndex, int? timeLimitMinutes, int passMark, params ExamTypeCount[] counts) => ExamBlueprint.CreateForUnit(Units[unitIndex], new ExamBlueprintShape(counts, null, timeLimitMinutes, passMark), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

    public ExamBlueprint DefaultBlueprint(int? timeLimitMinutes, int passMark, params ExamTypeCount[] counts) => ExamBlueprint.CreateForSubject(Subject, new ExamBlueprintShape(counts, null, timeLimitMinutes, passMark), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

    public Session Build(int perUnit = 10, int size = 20, int? timeLimitMinutes = 30, bool isTestMode = false)
    {
        var parts = Units
            .Select((unit, index) => new MultiUnitExamPart(unit.Id, UnitBlueprint(index, timeLimitMinutes, 50, new ExamTypeCount(QuestionType.Mcq, perUnit))))
            .ToList();
        var plan = MultiUnitBlueprintMerge.Merge(parts, size, MaxTimeLimitMinutes);
        var questions = Units
            .SelectMany((_, index) => Enumerable.Range(0, perUnit).Select(_ => Approved(index)))
            .ToList();
        return Session.StartMultiUnitExam(StudentId, Subject.Id, Units, plan, size, questions, Lessons, isTestMode, Now);
    }
}
