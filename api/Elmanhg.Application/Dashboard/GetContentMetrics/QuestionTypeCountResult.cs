using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Dashboard.GetContentMetrics;

public sealed record QuestionTypeCountResult(QuestionType Type, int Count);
