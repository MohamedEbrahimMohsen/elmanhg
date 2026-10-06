using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Students.SaveSubjectInterests;

public sealed class SaveSubjectInterestsValidator : AbstractValidator<SaveSubjectInterestsCommand>
{
    public SaveSubjectInterestsValidator(IOptions<StudentsOptions> studentsOptions)
    {
        var options = studentsOptions.Value;

        RuleFor(x => x.SubjectIds).ValidateListMaxItems(options.SubjectInterestsMaxCount, ErrorCodes.SubjectInterestsTooMany);
        RuleFor(x => x.SubjectIds)
            .ValidateDistinct(ErrorCodes.SubjectInterestsDuplicate);
        RuleForEach(x => x.SubjectIds).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
