using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.ExamBlueprints;

public sealed record ExamTypeShortfall(QuestionType Type, int Required, int Available)
{
    public int Missing => Required - Available;
}
