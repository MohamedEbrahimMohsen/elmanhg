using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.Shared;

public sealed class QuestionFieldsValidator : AbstractValidator<QuestionFields>
{
    public QuestionFieldsValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.Type)
            .ValidateRequired(ErrorCodes.QuestionTypeRequired)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionTypeInvalid);
        RuleFor(x => x.Stem)
            .ValidateRequired(ErrorCodes.QuestionStemRequired)
            .ValidateMaxLength(options.QuestionStemMaxLength, ErrorCodes.QuestionStemTooLong);
        RuleFor(x => x.Explanation).ValidateMaxLength(options.QuestionExplanationMaxLength, ErrorCodes.QuestionExplanationTooLong);
        RuleFor(x => x.Difficulty)
            .ValidateRequired(ErrorCodes.QuestionDifficultyRequired)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionDifficultyInvalid);
        RuleFor(x => x.MaxScore).ValidateRequired(ErrorCodes.QuestionMaxScoreRequired);
        RuleFor(x => x.MaxScore.GetValueOrDefault())
            .ValidateRange(1, options.QuestionMaxScoreMax, ErrorCodes.QuestionMaxScoreInvalid)
            .When(x => x.MaxScore.HasValue)
            .OverridePropertyName(nameof(QuestionFields.MaxScore));
        RuleFor(x => x.Tags).ValidateListMaxItems(options.QuestionTagsMaxCount, ErrorCodes.QuestionTagsTooMany);
        RuleForEach(x => x.Tags)
            .ValidateRequired(ErrorCodes.QuestionTagRequired)
            .ValidateMaxLength(options.QuestionTagMaxLength, ErrorCodes.QuestionTagTooLong);
        RuleFor(x => x)
            .Custom((fields, context) =>
            {
                foreach (var code in QuestionSchemaRules.Validate(fields, options))
                {
                    context.AddFailure(new ValidationFailure(nameof(QuestionFields.Body), code) { ErrorCode = code });
                }
            })
            .When(x => x.Type.HasValue && Enum.IsDefined(x.Type.Value));
    }
}
