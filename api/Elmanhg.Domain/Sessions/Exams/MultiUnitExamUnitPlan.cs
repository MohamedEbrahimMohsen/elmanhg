using Elmanhg.Domain.ExamBlueprints;

namespace Elmanhg.Domain.Sessions.Exams;

public sealed record MultiUnitExamUnitPlan(Guid UnitId, bool IsSubjectDefault, IReadOnlyList<ExamTypeCount> TypeCounts, ExamDifficultyMix? DifficultyMix)
{
    public int QuestionCount => TypeCounts.Sum(x => x.Count);
}
