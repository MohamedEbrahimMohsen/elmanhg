namespace Elmanhg.Application.Shared.Payments;

public interface IPaymentNotificationReader
{
    PaymentNotification? Read(string payload, string? signature);
}
