using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.Sessions.Exams;

public sealed record ExamCandidate(Guid QuestionId, Guid LessonId, QuestionType Type, QuestionDifficulty Difficulty);
