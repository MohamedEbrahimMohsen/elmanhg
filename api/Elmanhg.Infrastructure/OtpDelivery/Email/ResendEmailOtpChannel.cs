using Core.OTP;
using Core.OTP.Delivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Elmanhg.Infrastructure.OtpDelivery.Email;

public sealed class ResendEmailOtpChannel(HttpClient httpClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<OtpOptions> otpOptions, ILogger<ResendEmailOtpChannel> logger) : IOtpChannel
{
    private const string EmailsPath = "emails";
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string BearerScheme = "Bearer";

    public OtpChannel Channel => OtpChannel.Email;

    public async Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var email = otpDeliveryOptions.Value.Email;
        var minutes = otpOptions.Value.ExpirationMinutes;
        using var request = new HttpRequestMessage(HttpMethod.Post, EmailsPath)
        {
            Content = JsonContent.Create(new ResendEmailMessage(email.FromAddress, [recipient], email.Subject, OtpEmailTemplate.RenderHtml(code, minutes), OtpEmailTemplate.RenderText(code, minutes))),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, email.ApiKey);
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));
        await httpClient.SendOtpRequestAsync(request, Channel, logger, cancellationToken).ConfigureAwait(false);
    }
}
