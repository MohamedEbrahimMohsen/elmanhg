namespace Elmanhg.Application.Exams.Shared;

public sealed record ExamLessonResult(Guid LessonId, string? Name, int QuestionCount, int CorrectCount, decimal Score, int MaxScore, decimal ScorePercent);

public sealed record ExamObjectiveResult(Guid ObjectiveId, string Text, Guid LessonId, string? LessonName, int QuestionCount, decimal ScorePercent);

public sealed record ExamUnitBreakdownResult(Guid UnitId, string? Name, int QuestionCount, int CorrectCount, decimal Score, int MaxScore, decimal ScorePercent);
