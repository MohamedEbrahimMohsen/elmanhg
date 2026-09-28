using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Lessons.GetLessons;

public sealed class GetLessonsValidator : AbstractValidator<GetLessonsQuery>
{
    public GetLessonsValidator()
    {
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
    }
}
