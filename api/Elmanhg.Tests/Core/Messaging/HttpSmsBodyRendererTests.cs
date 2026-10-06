using Core.Messaging.Sms;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Messaging;

public sealed class HttpSmsBodyRendererTests
{
    [Fact]
    public void Render_Json_EscapesQuotesAndKeepsArabic()
    {
        var body = HttpSmsBodyRenderer.Render("{\"text\":\"{message}\"}", HttpSmsBodyRenderer.JsonContentType, "01012345678", "201012345678", "رمز \"1\"");

        body.Should().Be("{\"text\":\"رمز \\\"1\\\"\"}");
    }

    [Fact]
    public void Render_Form_UrlEncodesValues()
    {
        var body = HttpSmsBodyRenderer.Render("msg={message}", HttpSmsBodyRenderer.FormContentType, "01012345678", "201012345678", "a b&c");

        body.Should().Be("msg=a%20b%26c");
    }
}
