namespace Core.Messaging.WhatsApp;

public sealed record MetaWhatsAppSender(string ApiVersion, string PhoneNumberId, string AccessToken);
