using Core.OTP.Sms;
using System.Collections.Concurrent;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class RecordingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _latestCodes = new();

    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        _latestCodes[phoneNumber] = code;
        return Task.CompletedTask;
    }

    public string LatestCodeFor(string phoneNumber)
    {
        return _latestCodes.TryGetValue(phoneNumber, out var code) ? code : throw new InvalidOperationException($"No OTP was sent to {phoneNumber}.");
    }
}
