using Elmanhg.Application.Configuration.UpdateRuntimeSetting;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Configuration.UpdateRuntimeSetting;

public sealed class UpdateRuntimeSettingValidatorTests
{
    private readonly UpdateRuntimeSettingValidator _validator = new(FakeRuntimeSettings.DefaultRegistry());

    [Fact]
    public void Validate_ValidValue_Passes()
    {
        var result = _validator.Validate(Command("plans.freeDailyQuizQuestions", 12));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BlankKey_FailsKeyRequired()
    {
        var result = _validator.Validate(Command(" ", 12));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.RuntimeSettingKeyRequired);
    }

    [Fact]
    public void Validate_OutOfRange_FailsValueInvalid()
    {
        var result = _validator.Validate(Command("plans.freeDailyQuizQuestions", 1001));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.RuntimeSettingValueInvalid);
    }

    [Fact]
    public void Validate_WrongType_FailsValueInvalid()
    {
        var result = _validator.Validate(new UpdateRuntimeSettingCommand("plans.freeDailyQuizQuestions", RuntimeSettingJson.ToElement(true)));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.RuntimeSettingValueInvalid);
    }

    [Fact]
    public void Validate_UnknownKey_PassesValueRule()
    {
        var result = _validator.Validate(Command("nope.key", 12));

        result.Errors.Select(x => x.ErrorCode).Should().NotContain(ErrorCodes.RuntimeSettingValueInvalid);
    }

    private static UpdateRuntimeSettingCommand Command(string key, int value) => new(key, RuntimeSettingJson.ToElement(value));
}
