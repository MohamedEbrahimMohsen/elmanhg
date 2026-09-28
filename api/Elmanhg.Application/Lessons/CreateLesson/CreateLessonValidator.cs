using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Lessons.CreateLesson;

public sealed class CreateLessonValidator : AbstractValidator<CreateLessonCommand>
{
    public CreateLessonValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
        RuleFor(x => x.Name)
            .ValidateRequired(ErrorCodes.LessonNameRequired)
            .ValidateMaxLength(options.LessonNameMaxLength, ErrorCodes.LessonNameTooLong);
    }
}
