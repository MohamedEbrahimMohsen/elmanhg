using Core.OTP.Delivery;
using System.Collections.Concurrent;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class OtpOutbox
{
    private readonly ConcurrentDictionary<string, (OtpChannel Channel, string Code)> _latest = new();

    public void Record(OtpChannel channel, string recipient, string code)
    {
        _latest[recipient] = (channel, code);
    }

    public string LatestCodeFor(string recipient)
    {
        return Latest(recipient).Code;
    }

    public OtpChannel LatestChannelFor(string recipient)
    {
        return Latest(recipient).Channel;
    }

    private (OtpChannel Channel, string Code) Latest(string recipient)
    {
        return _latest.TryGetValue(recipient, out var sent) ? sent : throw new InvalidOperationException($"No OTP was sent to {recipient}.");
    }
}
