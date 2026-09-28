using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;

namespace Elmanhg.Tests.Builders;

public sealed class ExamSessionBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

    public ExamSessionBuilder()
    {
        Questions = new QuestionBuilder();
        Questions.Lesson.Publish(Guid.NewGuid());
    }

    public QuestionBuilder Questions { get; }

    public Guid StudentId { get; } = Guid.NewGuid();

    public List<Question> BuildQuestions(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => Questions.Approved().Build())
            .ToList();
    }

    public ExamBlueprint Blueprint(int mcqCount, int? timeLimitMinutes = 30, int passMark = 50) => ExamBlueprint.CreateForUnit(Questions.Unit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, mcqCount)], null, timeLimitMinutes, passMark), ExamBlueprintBuilder.Plenty(), Guid.NewGuid());

    public Session Build(int count = 2, int? timeLimitMinutes = 30, bool isTestMode = false) => Session.StartUnitExam(StudentId, Questions.Unit, Blueprint(count, timeLimitMinutes), BuildQuestions(count), [Questions.Lesson], isTestMode, Now);
}
