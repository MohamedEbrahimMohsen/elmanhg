using Elmanhg.Domain.Questions.Schemas;

namespace Elmanhg.Domain.Questions;

public sealed record QuestionContent(string Stem, string Body, string GradingSpec, string Explanation, int MaxScore)
{
    public bool IsEquivalentTo(QuestionContent other)
    {
        return Stem == other.Stem
            && Explanation == other.Explanation
            && MaxScore == other.MaxScore
            && QuestionJson.AreEquivalent(Body, other.Body)
            && QuestionJson.AreEquivalent(GradingSpec, other.GradingSpec);
    }
}
