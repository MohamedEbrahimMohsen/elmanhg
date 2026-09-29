using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Infrastructure.OtpDelivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed class PaymobPaymentGateway(HttpClient httpClient, IOptions<PaymentsOptions> paymentsOptions, ILogger<PaymobPaymentGateway> logger) : IPaymentGateway
{
    private const string IntentionPath = "v1/intention/";
    private const string TokenScheme = "Token";

    public bool SupportsSimulatedCompletion => false;

    public async Task<PaymentCheckout> StartCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        var paymob = paymentsOptions.Value.Paymob;
        using var message = new HttpRequestMessage(HttpMethod.Post, IntentionPath)
        {
            Content = JsonContent.Create(PaymobIntentionRequest.Create(request, paymob)),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(TokenScheme, paymob.SecretKey);
        message.Headers.UserAgent.ParseAdd(OtpProviderHttpExtensions.UserAgent);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(exception, "Paymob intention for payment {PaymentId} failed before a response arrived.", request.PaymentId);
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Paymob rejected the intention for payment {PaymentId} with HTTP {StatusCode}.", request.PaymentId, (int)response.StatusCode);
                throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
            }

            var intention = await ReadIntentionAsync(response, request.PaymentId, cancellationToken).ConfigureAwait(false);
            return new PaymentCheckout($"{paymob.CheckoutUrl}?publicKey={Uri.EscapeDataString(paymob.PublicKey)}&clientSecret={Uri.EscapeDataString(intention.ClientSecret!)}", ProviderOrderIdFrom(intention));
        }
    }

    private async Task<PaymobIntentionResponse> ReadIntentionAsync(HttpResponseMessage response, Guid paymentId, CancellationToken cancellationToken)
    {
        PaymobIntentionResponse? intention;
        try
        {
            intention = await response.Content.ReadFromJsonAsync<PaymobIntentionResponse>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Paymob returned an unreadable intention for payment {PaymentId}.", paymentId);
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable, innerException: exception);
        }

        if (string.IsNullOrWhiteSpace(intention?.ClientSecret))
        {
            logger.LogError("Paymob returned no client secret for payment {PaymentId}.", paymentId);
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
        }

        return intention;
    }

    private static string? ProviderOrderIdFrom(PaymobIntentionResponse intention) => intention.IntentionOrderId switch
    {
        { ValueKind: JsonValueKind.Number } number => number.GetRawText(),
        { ValueKind: JsonValueKind.String } text when !string.IsNullOrWhiteSpace(text.GetString()) => text.GetString(),
        _ => null,
    };
}
