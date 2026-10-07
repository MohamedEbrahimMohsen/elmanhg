using Core.Settings;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Shared.Options;

public sealed class RuntimeSettingsOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var result = Validator(new AskTeacherOptions()).Validate(null, new RuntimeSettingsOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_DefaultOutsideRuntimeRange_FailsNamingKey()
    {
        var result = Validator(new AskTeacherOptions { VoiceMaxSizeInMb = 20 }).Validate(null, new RuntimeSettingsOptions());

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("uploads.voiceReplyMaxSizeInMb");
    }

    private static RuntimeSettingsOptionsValidator Validator(AskTeacherOptions askTeacher)
    {
        var askTeacherOptions = Microsoft.Extensions.Options.Options.Create(askTeacher);
        var subscriptionsOptions = Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions());
        IRuntimeSettingDefinitions[] groups =
        [
            new FeatureFlagRuntimeSettings(Microsoft.Extensions.Options.Options.Create(new ExamsOptions())),
            new AskTeacherRuntimeSettings(askTeacherOptions, subscriptionsOptions),
            new PlanLimitRuntimeSettings(subscriptionsOptions),
            new GradingRuntimeSettings(Microsoft.Extensions.Options.Options.Create(new EssayGradingOptions()), Microsoft.Extensions.Options.Options.Create(new MathStepGradingOptions())),
            new UploadRuntimeSettings(askTeacherOptions),
        ];
        return new RuntimeSettingsOptionsValidator(groups, Enum.GetNames<RuntimeSettingGroup>());
    }
}
