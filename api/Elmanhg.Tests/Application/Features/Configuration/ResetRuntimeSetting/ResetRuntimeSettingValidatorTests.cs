using Elmanhg.Application.Configuration.ResetRuntimeSetting;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Configuration.ResetRuntimeSetting;

public sealed class ResetRuntimeSettingValidatorTests
{
    private readonly ResetRuntimeSettingValidator _validator = new();

    [Fact]
    public void Validate_Key_Passes()
    {
        var result = _validator.Validate(new ResetRuntimeSettingCommand("plans.freeDailyQuizQuestions"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BlankKey_FailsKeyRequired()
    {
        var result = _validator.Validate(new ResetRuntimeSettingCommand(" "));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.RuntimeSettingKeyRequired);
    }
}
