using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared.Grading;

public static class AnswerGrader
{
    public static Task<AnswerDecision> DecideAsync(QuestionRevision revision, JsonElement answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)
    {
        var snapshot = revision.ReadSnapshot();
        return DecideAsync(snapshot.Type, snapshot.GradingSpec?.ToJsonString() ?? "{}", snapshot.MaxScore, answer, mathCheckClient, cancellationToken);
    }

    public static async Task<AnswerDecision> DecideAsync(QuestionType type, string gradingSpec, int maxScore, JsonElement answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)
    {
        if (type != QuestionType.MathSteps)
        {
            return AnswerDecision.Graded(QuestionGrader.Grade(type, gradingSpec, maxScore, answer));
        }

        var mathAnswer = QuestionSchemaReader.Read<MathStepsAnswer>(answer);
        var finalAnswer = (mathAnswer.FinalAnswer ?? string.Empty).Trim();
        if (finalAnswer.Length == 0)
        {
            return AnswerDecision.Graded(QuestionGrader.GradeMathSteps(maxScore, null));
        }

        var verdict = await CheckFinalAnswerAsync(gradingSpec, finalAnswer, mathCheckClient, cancellationToken).ConfigureAwait(false);
        if (verdict == MathAnswerVerdict.Unchecked)
        {
            return AnswerDecision.Deferred(null);
        }

        return MathStepsGrader.NeedsStepGrading(ReadSpec(gradingSpec), mathAnswer) ? AnswerDecision.Deferred(verdict) : AnswerDecision.Graded(QuestionGrader.GradeMathStepsCombined(gradingSpec, maxScore, verdict, null));
    }

    public static async Task<MathAnswerVerdict> CheckFinalAnswerAsync(string gradingSpec, string finalAnswer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)
    {
        var spec = ReadSpec(gradingSpec);
        var request = new AiMathCheckRequest(finalAnswer, spec.AcceptedAnswers ?? [], spec.Form ?? MathAnswerForm.Equivalent, spec.Tolerance, spec.ToleranceMode);
        var result = await mathCheckClient.CheckAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Verdict;
    }

    private static MathStepsGradingSpec ReadSpec(string gradingSpec) => JsonSerializer.Deserialize<MathStepsGradingSpec>(gradingSpec, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Question grading spec is not readable.");
}
