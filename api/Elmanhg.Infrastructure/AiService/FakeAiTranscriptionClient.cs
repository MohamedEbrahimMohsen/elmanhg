using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Hosting;

namespace Elmanhg.Infrastructure.AiService;

public sealed class FakeAiTranscriptionClient(IHostEnvironment hostEnvironment) : IAiTranscriptionClient
{
    public const string FakeTranscript = "هذا تفريغ تجريبي للرد الصوتي.";
    public const string FakeModel = "fake";

    public Task<AiTranscriptionResult> TranscribeAsync(AiTranscriptionRequest request, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsProduction())
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return Task.FromResult(new AiTranscriptionResult(FakeTranscript, FakeModel, request.Language));
    }
}
