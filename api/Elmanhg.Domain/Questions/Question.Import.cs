using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Units;

namespace Elmanhg.Domain.Questions;

public partial class Question
{
    public static Question CreateImported(Lesson lesson, CurriculumUnit unit, QuestionType type, QuestionContent content, QuestionMetadata metadata, Guid importBatchId, Guid createdBy)
    {
        var question = Create(lesson, unit, type, content, metadata, createdBy);
        question.ImportBatchId = importBatchId;
        return question;
    }
}
