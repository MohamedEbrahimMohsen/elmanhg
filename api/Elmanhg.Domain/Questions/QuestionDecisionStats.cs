namespace Elmanhg.Domain.Questions;

public sealed record QuestionDecisionStats
{
    public int Approved { get; init; }
    public int Rejected { get; init; }
    public double? MedianSecondsToDecision { get; init; }
}
