using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.Shared.AiService;

public sealed record AiMathCheckResult(MathAnswerVerdict Verdict, int? MatchedIndex, IReadOnlyList<int> InvalidExpected);
