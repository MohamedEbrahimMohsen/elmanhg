using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.QuestionValidation.BulkApproveQuestions;

public sealed class BulkApproveQuestionsValidator : AbstractValidator<BulkApproveQuestionsCommand>
{
    public BulkApproveQuestionsValidator(IOptions<QuestionValidationOptions> questionValidationOptions)
    {
        var options = questionValidationOptions.Value;

        RuleFor(x => x.ReviewSessionId).ValidateRequired(ErrorCodes.ReviewSessionIdRequired);
        RuleFor(x => x.QuestionIds)
            .ValidateNotEmptyList(ErrorCodes.QuestionIdsRequired)
            .ValidateListMaxItems(options.BulkApproveMaxCount, ErrorCodes.QuestionIdsTooMany);
        RuleFor(x => x.QuestionIds)
            .ValidateDistinct(ErrorCodes.QuestionIdsDuplicate);
        RuleForEach(x => x.QuestionIds).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
