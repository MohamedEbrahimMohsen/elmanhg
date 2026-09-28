using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.OtpDelivery.Email;

public sealed record ResendEmailMessage([property: JsonPropertyName("from")] string From, [property: JsonPropertyName("to")] List<string> To, [property: JsonPropertyName("subject")] string Subject, [property: JsonPropertyName("html")] string Html, [property: JsonPropertyName("text")] string Text);
