using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.GetVoiceReplySettings;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.GetVoiceReplySettings;

public sealed class GetVoiceReplySettingsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConfiguredLimits()
    {
        var handler = new GetVoiceReplySettingsHandler(Options.Create(new AskTeacherOptions { VoiceMaxDurationSeconds = 180, VoiceMaxSizeInMb = 5 }));

        var result = await handler.Handle(new GetVoiceReplySettingsQuery(), TestContext.Current.CancellationToken);

        result.Should().Be(new VoiceReplySettingsResult(180, 5));
    }
}
