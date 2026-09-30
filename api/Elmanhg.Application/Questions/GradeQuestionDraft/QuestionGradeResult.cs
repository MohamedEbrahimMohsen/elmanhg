using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.MathStepGrading.Shared;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed record QuestionGradeResult(decimal Score, decimal NormalisedScore, string Outcome, int MaxScore, string? Feedback, EssayGradeDetailResult? Essay = null, MathStepGradeDetailResult? MathSteps = null);
