namespace Elmanhg.Application.Exams.Shared;

public sealed record MultiUnitExamPreviewResult(Guid SubjectId, int Size, ExamBlueprintSummaryResult Blueprint, bool IsAvailable, List<MultiUnitExamUnitShareResult> Units);

public sealed record MultiUnitExamUnitShareResult(Guid UnitId, string Name, int QuestionCount, bool IsSubjectDefault);
