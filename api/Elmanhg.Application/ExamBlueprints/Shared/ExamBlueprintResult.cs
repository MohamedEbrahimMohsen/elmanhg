using Core.Auditing;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.ExamBlueprints.Shared;

public sealed record ExamBlueprintResult(Guid Id, Guid SubjectId, Guid? UnitId, List<ExamTypeCountResult> TypeCounts, ExamDifficultyMixResult? DifficultyMix, int QuestionCount, int? TimeLimitMinutes, int PassMark) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}

public sealed record ExamTypeCountResult(QuestionType Type, int Count);

public sealed record ExamDifficultyMixResult(int EasyPercent, int MediumPercent, int HardPercent);
