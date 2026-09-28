using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.ExamBlueprints;

public sealed record ExamTypeCount(QuestionType Type, int Count);
