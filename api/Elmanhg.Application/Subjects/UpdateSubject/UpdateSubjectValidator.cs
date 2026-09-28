using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subjects.UpdateSubject;

public sealed class UpdateSubjectValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.Name)
            .ValidateRequired(ErrorCodes.SubjectNameRequired)
            .ValidateMaxLength(options.SubjectNameMaxLength, ErrorCodes.SubjectNameTooLong);
    }
}
