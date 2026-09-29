using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Browse.RecordLessonOpening;

public sealed class RecordLessonOpeningValidator : AbstractValidator<RecordLessonOpeningCommand>
{
    public RecordLessonOpeningValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
    }
}
