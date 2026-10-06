using Elmanhg.Infrastructure.OtpDelivery.Email;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.OtpDelivery;

public sealed class OtpEmailTemplateTests
{
    [Fact]
    public void Render_Code_ReturnsHtmlAndTextWithCode()
    {
        var content = OtpEmailTemplate.Render("482913", 7);

        content.Html.Should().Contain("dir=\"rtl\"").And.Contain("482913");
        content.Text.Should().Contain("482913").And.Contain("7");
    }
}
