using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Application.Shared.AiService;

public sealed record AiMathCheckRequest(string Answer, IReadOnlyList<string> Expected, MathAnswerForm Form, decimal? Tolerance, ToleranceMode? ToleranceMode);
