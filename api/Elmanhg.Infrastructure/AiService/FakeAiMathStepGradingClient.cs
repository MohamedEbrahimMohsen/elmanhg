using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;
using Microsoft.Extensions.Hosting;

namespace Elmanhg.Infrastructure.AiService;

public sealed class FakeAiMathStepGradingClient(IHostEnvironment hostEnvironment) : IAiMathStepGradingClient
{
    public const string FakeJustification = "هذا تصحيح تجريبي لخطوات الحل.";
    public const string FakeStepJustification = "تصحيح تجريبي: الخطوة صحيحة.";
    public const string FakeModel = "fake";
    public const string FakePromptVersion = "fake";
    public const decimal FakeConfidence = 0.9m;
    private const string FakeStopReason = "end_turn";

    public Task<AiMathStepGradingResult> GradeAsync(AiMathStepGradingRequest request, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsProduction())
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.MathStepGradingUnavailable);
        }

        var steps = request.ModelSolution
            .Select((_, index) => new AiMathStepScore(index, MathStepsGrader.MaxStepPoints, FakeStepJustification))
            .ToList();
        var total = MathStepsGrader.MaxStepPoints * steps.Count;
        return Task.FromResult(new AiMathStepGradingResult(steps, total, total, FakeJustification, FakeConfidence, FakeModel, FakePromptVersion, 0, 0, FakeStopReason, 0m));
    }
}
