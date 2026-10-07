using Core.Notifications.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;

namespace Core.Notifications.RegisterUserDevice;

public sealed class RegisterUserDeviceValidator : AbstractValidator<RegisterUserDeviceCommand>
{
    public RegisterUserDeviceValidator()
    {
        RuleFor(x => x.DeviceId)
            .ValidateRequired(ErrorCodes.UserDeviceIdRequired);

        RuleFor(x => x.PushToken)
            .ValidateRequired(ErrorCodes.PushTokenRequired);

    }
}
