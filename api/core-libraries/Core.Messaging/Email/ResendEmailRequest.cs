using System.Text.Json.Serialization;

namespace Core.Messaging.Email;

public sealed record ResendEmailRequest([property: JsonPropertyName("from")] string From, [property: JsonPropertyName("to")] List<string> To, [property: JsonPropertyName("subject")] string Subject, [property: JsonPropertyName("html")] string Html, [property: JsonPropertyName("text")] string Text)
{
    public static ResendEmailRequest Create(EmailMessage message) => new(message.From, [message.To], message.Subject, message.Html, message.Text);
}
