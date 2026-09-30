using Elmanhg.Infrastructure.Invitations;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Infrastructure.Invitations;

public sealed class InvitationEmailOptionsValidatorTests
{
    [Fact]
    public void Validate_FakeEmailWithoutLink_Succeeds()
    {
        var validator = new InvitationEmailOptionsValidator(Options.Create(OtpDeliveryTestSettings.Fake()));

        var result = validator.Validate(null, new InvitationEmailOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_ResendWithoutHttpsLink_Fails()
    {
        var validator = new InvitationEmailOptionsValidator(Options.Create(OtpDeliveryTestSettings.WithResend()));

        var result = validator.Validate(null, new InvitationEmailOptions { AcceptInviteUrl = "http://elmanhg.test/accept-invite" });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("InvitationEmail:AcceptInviteUrl");
    }

    [Fact]
    public void Validate_ResendWithHttpsLink_Succeeds()
    {
        var validator = new InvitationEmailOptionsValidator(Options.Create(OtpDeliveryTestSettings.WithResend()));

        var result = validator.Validate(null, new InvitationEmailOptions { AcceptInviteUrl = "https://elmanhg.test/accept-invite" });

        result.Succeeded.Should().BeTrue();
    }
}
