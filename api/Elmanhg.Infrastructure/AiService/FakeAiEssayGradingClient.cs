using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Hosting;

namespace Elmanhg.Infrastructure.AiService;

public sealed class FakeAiEssayGradingClient(IHostEnvironment hostEnvironment) : IAiEssayGradingClient
{
    public const string FakeJustification = "هذا تصحيح تجريبي من مصحح الذكاء الاصطناعي.";
    public const string FakeCriterionJustification = "تصحيح تجريبي: المعيار مستوفى.";
    public const string FakeModel = "fake";
    public const string FakePromptVersion = "fake";
    public const decimal FakeConfidence = 0.9m;
    private const string FakeStopReason = "end_turn";

    public Task<AiEssayGradingResult> GradeAsync(AiEssayGradingRequest request, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsProduction())
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable);
        }

        var criteria = request.Criteria
            .Select(x => new AiEssayCriterionScore(x.Id, x.Points, FakeCriterionJustification))
            .ToList();
        var total = request.Criteria.Sum(x => x.Points);
        return Task.FromResult(new AiEssayGradingResult(criteria, total, total, FakeJustification, FakeConfidence, FakeModel, FakePromptVersion, 0, 0, FakeStopReason, 0m));
    }
}
