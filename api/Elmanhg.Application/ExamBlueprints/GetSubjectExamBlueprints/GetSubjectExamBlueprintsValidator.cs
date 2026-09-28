using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.ExamBlueprints.GetSubjectExamBlueprints;

public sealed class GetSubjectExamBlueprintsValidator : AbstractValidator<GetSubjectExamBlueprintsQuery>
{
    public GetSubjectExamBlueprintsValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
