namespace Elmanhg.Application.Exams.Shared;

public sealed record MultiUnitExamOverviewResult(Guid SubjectId, string SubjectName, List<MultiUnitExamUnitOptionResult> Units, List<int> Sizes, InProgressExamResult? InProgressExam);

public sealed record MultiUnitExamUnitOptionResult(Guid UnitId, string Name, bool HasBlueprint, bool IsSubjectDefault, int ServableCount);
