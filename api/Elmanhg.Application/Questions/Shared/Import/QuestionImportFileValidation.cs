using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportFileValidation
{
    public static IRuleBuilderOptions<T, IFormFile?> ValidateQuestionImportFile<T>(this IRuleBuilderInitial<T, IFormFile?> rule, ContentOptions options)
    {
        return rule
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithErrorCode(ErrorCodes.QuestionImportFileRequired)
            .ValidateRequired(ErrorCodes.QuestionImportFileRequired)
            .ValidateAllowedExtensions(QuestionImportFile.Extensions, ErrorCodes.QuestionImportFileTypeInvalid)
            .ValidateMaxFileSize(options.QuestionImportMaxFileSizeInMb, ErrorCodes.QuestionImportFileTooLarge)
            .Must(file => file is not null && QuestionImportFile.HasZipSignature(file))
            .WithErrorCode(ErrorCodes.QuestionImportFileTypeInvalid);
    }
}
