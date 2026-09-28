using Core.OTP.Delivery;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class RecordingOtpChannel(OtpChannel channel, OtpOutbox outbox) : IOtpChannel
{
    public OtpChannel Channel => channel;

    public Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        outbox.Record(channel, recipient, code);
        return Task.CompletedTask;
    }
}
