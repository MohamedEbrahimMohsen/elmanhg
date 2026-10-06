using Core.Messaging.WhatsApp;
using FluentAssertions;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Messaging;

public sealed class MetaWhatsAppTemplateMessageTests
{
    [Fact]
    public void Create_WithButtonSuffix_AddsUrlButtonComponent()
    {
        var message = MetaWhatsAppTemplateMessage.Create("201012345678", "probe_template", "ar", ["Physics", "Newton"], "thread-1");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(message));

        var button = json.RootElement.GetProperty("template").GetProperty("components")[1];
        (button.GetProperty("type").GetString(), button.GetProperty("sub_type").GetString(), button.GetProperty("index").GetString()).Should().Be(("button", "url", "0"));
        button.GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be("thread-1");
    }

    [Fact]
    public void Create_WithoutButtonSuffix_SendsBodyWithoutSubTypeOrIndex()
    {
        var message = MetaWhatsAppTemplateMessage.Create("201012345678", "probe_template", "ar", ["482913"], null);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(message));

        var components = json.RootElement.GetProperty("template").GetProperty("components");
        components.GetArrayLength().Should().Be(1);
        components[0].TryGetProperty("sub_type", out _).Should().BeFalse();
        components[0].TryGetProperty("index", out _).Should().BeFalse();
    }
}
