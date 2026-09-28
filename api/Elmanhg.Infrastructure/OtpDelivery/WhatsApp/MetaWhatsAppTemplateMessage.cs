using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.OtpDelivery.WhatsApp;

public sealed record MetaWhatsAppTemplateMessage([property: JsonPropertyName("messaging_product")] string MessagingProduct, [property: JsonPropertyName("recipient_type")] string RecipientType, [property: JsonPropertyName("to")] string To, [property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("template")] MetaWhatsAppTemplate Template)
{
    private const string WhatsAppProduct = "whatsapp";
    private const string IndividualRecipient = "individual";
    private const string TemplateType = "template";
    private const string BodyComponent = "body";
    private const string ButtonComponent = "button";
    private const string UrlButton = "url";
    private const string CopyCodeButtonIndex = "0";
    private const string TextParameter = "text";

    public static MetaWhatsAppTemplateMessage Create(string to, string code, WhatsAppOtpOptions options)
    {
        List<MetaWhatsAppComponent> components = [new(BodyComponent, null, null, [new(TextParameter, code)])];
        if (options.CopyCodeButton)
        {
            components.Add(new(ButtonComponent, UrlButton, CopyCodeButtonIndex, [new(TextParameter, code)]));
        }

        return new(MessagingProduct: WhatsAppProduct, RecipientType: IndividualRecipient, To: to, Type: TemplateType, Template: new(options.TemplateName, new(options.LanguageCode), components));
    }
}

public sealed record MetaWhatsAppTemplate([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("language")] MetaWhatsAppLanguage Language, [property: JsonPropertyName("components")] List<MetaWhatsAppComponent> Components);

public sealed record MetaWhatsAppLanguage([property: JsonPropertyName("code")] string Code);

public sealed record MetaWhatsAppComponent([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("sub_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SubType, [property: JsonPropertyName("index"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Index, [property: JsonPropertyName("parameters")] List<MetaWhatsAppParameter> Parameters);

public sealed record MetaWhatsAppParameter([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string Text);
