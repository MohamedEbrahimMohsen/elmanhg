namespace Core.Messaging.Sms;

public sealed record HttpSmsGateway(string Url, string ContentType, string BodyTemplate, string AuthHeaderName, string AuthHeaderValue);
