using Core.Errors;
using Core.Http;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed partial class PaymobPaymentGateway
{
    private const string RefundPath = "api/acceptance/void_refund/refund";

    public async Task<PaymentRefund> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, RefundPath)
        {
            Content = JsonContent.Create(new PaymobRefundRequest(request.ProviderTransactionId, request.Amount.AmountMinor)),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(TokenScheme, paymentsOptions.Value.Paymob.SecretKey);
        message.WithUserAgent(coreHttpOptions.Value.UserAgent);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception.IsTransientFailure(cancellationToken))
        {
            logger.LogError(exception, "Paymob refund for payment {PaymentId} failed before a response arrived.", request.PaymentId);
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable, innerException: exception);
        }

        using (response)
        {
            var status = response.StatusCode;
            if ((int)status >= 500 || status == HttpStatusCode.TooManyRequests)
            {
                logger.LogError("Paymob could not take the refund for payment {PaymentId}: HTTP {StatusCode}.", request.PaymentId, (int)status);
                throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Paymob declined the refund for payment {PaymentId} with HTTP {StatusCode}.", request.PaymentId, (int)status);
                throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundDeclined);
            }

            var refund = await ReadRefundAsync(response, request.PaymentId, cancellationToken).ConfigureAwait(false);
            return new PaymentRefund(refund);
        }
    }

    private async Task<string> ReadRefundAsync(HttpResponseMessage response, Guid paymentId, CancellationToken cancellationToken)
    {
        PaymobRefundResponse? refund;
        try
        {
            refund = await response.Content.ReadFromJsonAsync<PaymobRefundResponse>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Paymob returned an unreadable refund for payment {PaymentId}.", paymentId);
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable, innerException: exception);
        }

        if (refund?.Success == false)
        {
            logger.LogWarning("Paymob declined the refund for payment {PaymentId}.", paymentId);
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundDeclined);
        }

        var refundId = refund?.Id switch
        {
            { ValueKind: JsonValueKind.Number } number => number.GetRawText(),
            { ValueKind: JsonValueKind.String } text when !string.IsNullOrWhiteSpace(text.GetString()) => text.GetString(),
            _ => null,
        };
        if (refundId is null)
        {
            logger.LogError("Paymob returned no refund id for payment {PaymentId}.", paymentId);
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
        }

        if (refund!.Success != true || refund.Pending == true)
        {
            logger.LogWarning("Paymob did not complete the refund for payment {PaymentId}.", paymentId);
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundDeclined);
        }

        return refundId;
    }
}
