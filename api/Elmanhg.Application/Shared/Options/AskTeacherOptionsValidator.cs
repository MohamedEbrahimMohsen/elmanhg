using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.Options;

public sealed class AskTeacherOptionsValidator(IOptions<SubscriptionsOptions> subscriptionsOptions) : IValidateOptions<AskTeacherOptions>
{
    public ValidateOptionsResult Validate(string? name, AskTeacherOptions options)
    {
        var windowHours = subscriptionsOptions.Value.AskTeacherReplySlaHours;
        return options.FirstReminderAfterHours < options.SecondReminderAfterHours && options.SecondReminderAfterHours < windowHours
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"AskTeacher:FirstReminderAfterHours ({options.FirstReminderAfterHours}) < AskTeacher:SecondReminderAfterHours ({options.SecondReminderAfterHours}) < Subscriptions:AskTeacherReplySlaHours ({windowHours}) is required.");
    }
}
