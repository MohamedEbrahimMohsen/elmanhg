using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Configuration.ResetRuntimeSetting;

public sealed class ResetRuntimeSettingValidator : AbstractValidator<ResetRuntimeSettingCommand>
{
    public ResetRuntimeSettingValidator()
    {
        RuleFor(x => x.Key).ValidateRequired(ErrorCodes.RuntimeSettingKeyRequired);
    }
}
