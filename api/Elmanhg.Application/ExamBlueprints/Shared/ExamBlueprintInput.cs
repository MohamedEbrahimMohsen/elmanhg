using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.ExamBlueprints.Shared;

public sealed record ExamBlueprintInput(List<ExamTypeCountInput> TypeCounts, ExamDifficultyMixInput? DifficultyMix, int? TimeLimitMinutes, int PassMark);

public sealed record ExamTypeCountInput(QuestionType? Type, int Count);

public sealed record ExamDifficultyMixInput(int EasyPercent, int MediumPercent, int HardPercent);
