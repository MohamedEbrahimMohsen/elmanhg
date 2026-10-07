using Core.Settings;
using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Configuration.UpdateRuntimeSetting;

public sealed class UpdateRuntimeSettingValidator : AbstractValidator<UpdateRuntimeSettingCommand>
{
    public UpdateRuntimeSettingValidator(RuntimeSettingRegistry registry)
    {
        RuleFor(x => x.Key).ValidateRequired(ErrorCodes.RuntimeSettingKeyRequired);
        RuleFor(x => x)
            .Must(x => registry.Find(x.Key) is not { } definition || RuntimeSettingValueRules.IsValid(definition, x.Value))
            .WithErrorCode(ErrorCodes.RuntimeSettingValueInvalid);
    }
}
