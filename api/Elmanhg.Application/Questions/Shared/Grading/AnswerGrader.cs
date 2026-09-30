using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared.Grading;

public static class AnswerGrader
{
    public static Task<QuestionGrade> GradeAsync(QuestionRevision revision, JsonElement answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)
    {
        var snapshot = revision.ReadSnapshot();
        return GradeAsync(snapshot.Type, snapshot.GradingSpec?.ToJsonString() ?? "{}", snapshot.MaxScore, answer, mathCheckClient, cancellationToken);
    }

    public static async Task<QuestionGrade> GradeAsync(QuestionType type, string gradingSpec, int maxScore, JsonElement answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)
    {
        if (type != QuestionType.MathSteps)
        {
            return QuestionGrader.Grade(type, gradingSpec, maxScore, answer);
        }

        var finalAnswer = (QuestionSchemaReader.Read<MathStepsAnswer>(answer).FinalAnswer ?? string.Empty).Trim();
        if (finalAnswer.Length == 0)
        {
            return QuestionGrader.GradeMathSteps(maxScore, null);
        }

        var spec = JsonSerializer.Deserialize<MathStepsGradingSpec>(gradingSpec, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Question grading spec is not readable.");
        var request = new AiMathCheckRequest(finalAnswer, spec.AcceptedAnswers ?? [], spec.Form ?? MathAnswerForm.Equivalent, spec.Tolerance, spec.ToleranceMode);
        var result = await mathCheckClient.CheckAsync(request, cancellationToken).ConfigureAwait(false);
        return QuestionGrader.GradeMathSteps(maxScore, result.Verdict);
    }
}
