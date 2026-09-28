using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.ImportQuestions;

public sealed class ImportQuestionsValidator : AbstractValidator<ImportQuestionsCommand>
{
    public ImportQuestionsValidator(IOptions<ContentOptions> contentOptions)
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.BatchId).ValidateRequired(ErrorCodes.QuestionImportBatchIdRequired);
        RuleFor(x => x.File).ValidateQuestionImportFile(contentOptions.Value);
    }
}
