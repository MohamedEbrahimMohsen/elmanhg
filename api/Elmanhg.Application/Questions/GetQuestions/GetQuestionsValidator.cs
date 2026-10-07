using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.GetQuestions;

public sealed class GetQuestionsValidator : AbstractValidator<GetQuestionsQuery>
{
    public GetQuestionsValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.QuestionListMaxPageSize, ErrorCodes.QuestionPageNumberInvalid, ErrorCodes.QuestionPageSizeInvalid);
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionStatusInvalid);
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionTypeInvalid);
        RuleFor(x => x.MinVersion.GetValueOrDefault())
            .ValidateMin(1, ErrorCodes.QuestionVersionFilterInvalid)
            .When(x => x.MinVersion.HasValue)
            .OverridePropertyName(nameof(GetQuestionsQuery.MinVersion));
        RuleFor(x => x.RejectionReason).ValidateMaxLength(options.QuestionFilterMaxLength, ErrorCodes.QuestionFilterTooLong);
    }
}
