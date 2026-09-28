using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.QuestionValidation.GetValidationQueue;

public sealed class GetValidationQueueValidator : AbstractValidator<GetValidationQueueQuery>
{
    public GetValidationQueueValidator(IOptions<QuestionValidationOptions> questionValidationOptions)
    {
        var options = questionValidationOptions.Value;

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.QuestionPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.QueueMaxPageSize, ErrorCodes.QuestionPageSizeInvalid);
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionTypeInvalid);
        RuleFor(x => x.Difficulty)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionDifficultyInvalid);
        RuleFor(x => x.MinAgeDays.GetValueOrDefault())
            .ValidateRange(1, options.QueueMaxAgeDays, ErrorCodes.QuestionAgeFilterInvalid)
            .When(x => x.MinAgeDays.HasValue)
            .OverridePropertyName(nameof(GetValidationQueueQuery.MinAgeDays));
    }
}
