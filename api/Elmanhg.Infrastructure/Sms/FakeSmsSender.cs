using Core.OTP.Sms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Infrastructure.Sms;

public sealed class FakeSmsSender(ILogger<FakeSmsSender> logger, IHostEnvironment hostEnvironment) : ISmsSender
{
    public Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsDevelopment())
        {
            logger.LogInformation("FakeSmsSender OTP for {PhoneNumber}: {Code}", phoneNumber, code);
        }
        else
        {
            logger.LogWarning("FakeSmsSender is active outside Development; the OTP was not delivered.");
        }

        return Task.CompletedTask;
    }
}
