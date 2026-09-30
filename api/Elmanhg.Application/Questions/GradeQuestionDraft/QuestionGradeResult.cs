using Elmanhg.Application.EssayGrading.Shared;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed record QuestionGradeResult(decimal Score, decimal NormalisedScore, string Outcome, int MaxScore, string? Feedback, EssayGradeDetailResult? Essay = null);
