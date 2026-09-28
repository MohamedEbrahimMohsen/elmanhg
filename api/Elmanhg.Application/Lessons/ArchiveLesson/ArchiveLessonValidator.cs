using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Lessons.ArchiveLesson;

public sealed class ArchiveLessonValidator : AbstractValidator<ArchiveLessonCommand>
{
    public ArchiveLessonValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
    }
}
