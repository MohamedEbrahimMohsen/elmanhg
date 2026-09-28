using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subjects.CreateSubject;

public sealed class CreateSubjectValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.Name)
            .ValidateRequired(ErrorCodes.SubjectNameRequired)
            .ValidateMaxLength(options.SubjectNameMaxLength, ErrorCodes.SubjectNameTooLong);
    }
}
