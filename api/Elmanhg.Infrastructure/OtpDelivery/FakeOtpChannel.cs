using Core.OTP.Delivery;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Infrastructure.OtpDelivery;

public sealed class FakeOtpChannel(OtpChannel channel, ILogger<FakeOtpChannel> logger, IHostEnvironment hostEnvironment) : IOtpChannel
{
    public OtpChannel Channel => channel;

    public Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsDevelopment())
        {
            logger.LogInformation("FakeOtpChannel {Channel} OTP for {Recipient}: {Code}", channel, recipient, code);
        }
        else
        {
            logger.LogWarning("FakeOtpChannel {Channel} is active outside Development; the OTP was not delivered.", channel);
        }

        return Task.CompletedTask;
    }
}
