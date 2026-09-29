using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Hosting;

namespace Elmanhg.Infrastructure.AiService;

public sealed class FakeAiServiceClient(IHostEnvironment hostEnvironment) : IAiServiceClient
{
    public const string FakeReply = "هذا رد تجريبي من المساعد.";
    public const string FakeModel = "fake";
    public const string FakePromptVersion = "fake";

    public Task<AiChatReply> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsProduction())
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return Task.FromResult(new AiChatReply(FakeReply, FakeModel, FakePromptVersion, 0, 0, "end_turn"));
    }
}
