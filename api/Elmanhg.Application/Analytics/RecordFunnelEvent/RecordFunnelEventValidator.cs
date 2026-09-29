using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Analytics.RecordFunnelEvent;

public sealed class RecordFunnelEventValidator : AbstractValidator<RecordFunnelEventCommand>
{
    public RecordFunnelEventValidator()
    {
        RuleFor(x => x.AnonymousId).ValidateRequired(ErrorCodes.FunnelAnonymousIdRequired);
        RuleFor(x => x.Type).IsInEnum().WithErrorCode(ErrorCodes.FunnelEventTypeInvalid);
    }
}
