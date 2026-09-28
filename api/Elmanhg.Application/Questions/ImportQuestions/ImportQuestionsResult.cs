namespace Elmanhg.Application.Questions.ImportQuestions;

public sealed record ImportQuestionsResult(Guid BatchId, int CreatedCount, bool Replayed);
