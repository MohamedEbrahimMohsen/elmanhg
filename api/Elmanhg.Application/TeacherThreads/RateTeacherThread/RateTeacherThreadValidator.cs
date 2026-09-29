using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.TeacherThreads;
using FluentValidation;

namespace Elmanhg.Application.TeacherThreads.RateTeacherThread;

public sealed class RateTeacherThreadValidator : AbstractValidator<RateTeacherThreadCommand>
{
    public RateTeacherThreadValidator()
    {
        RuleFor(x => x.Rating).ValidateRange(TeacherThread.MinRating, TeacherThread.MaxRating, ErrorCodes.TeacherThreadRatingInvalid);
    }
}
