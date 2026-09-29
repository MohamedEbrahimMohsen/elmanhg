using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Exams.Shared;

public sealed record UnitExamOverviewResult(Guid UnitId, string UnitName, Guid SubjectId, string SubjectName, ExamBlueprintSummaryResult? Blueprint, bool IsAvailable, InProgressExamResult? InProgressExam, int UnopenedLessonCount);

public sealed record ExamBlueprintSummaryResult(bool IsSubjectDefault, int QuestionCount, List<ExamTypeAvailabilityResult> TypeCounts, ExamDifficultyMixResult? DifficultyMix, int? TimeLimitMinutes, int PassMark);

public sealed record ExamTypeAvailabilityResult(QuestionType Type, int Required, int Available);

public sealed record InProgressExamResult(Guid SessionId, bool IsThisUnit);
