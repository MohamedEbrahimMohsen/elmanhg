using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.Sessions;

public partial class Session
{
    public static Session StartMultiUnitExam(Guid studentId, Guid subjectId, IReadOnlyList<CurriculumUnit> units, MultiUnitExamPlan plan, int size, IReadOnlyList<Question> questions, IReadOnlyCollection<Lesson> lessons, bool isTestMode, DateTimeOffset now)
    {
        if (units.Count < MultiUnitExamSizes.MinUnits || units.Any(x => x.SubjectId != subjectId))
        {
            throw new InvalidOperationException("A multi-unit exam needs two or more units of one subject.");
        }

        var unitIds = units
            .Select(x => x.Id)
            .ToHashSet();
        EnsureExamQuestions(questions, x => IsServableInUnits(x, unitIds, lessons));
        var scope = new MultiUnitExamScope(subjectId, units.Select(x => x.Id).ToList(), size);
        return CreateExam(studentId, SessionKind.MultiUnitExam, scope.ToJson(), scope.ToKey(), plan.TimeLimitMinutes, plan.PassMark, questions, isTestMode, now);
    }

    public IReadOnlyList<Guid> GetExamUnitIds() => Kind switch
    {
        SessionKind.UnitExam => [UnitExamScope.FromJson(Scope).UnitId],
        SessionKind.MultiUnitExam => MultiUnitExamScope.FromJson(Scope).UnitIds,
        _ => [],
    };
}
