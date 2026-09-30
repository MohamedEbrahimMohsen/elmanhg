using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftValidator : AbstractValidator<GradeQuestionDraftQuery>
{
    public GradeQuestionDraftValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;
        RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions));
        RuleFor(x => x.Question.Type)
            .Must(x => x != QuestionType.DragDrop)
            .WithErrorCode(ErrorCodes.QuestionTypeNotGradable);
        RuleFor(x => x)
            .Must(x => QuestionAnswerRules.CanRead(x.Question.Type.GetValueOrDefault(), x.Answer))
            .WithErrorCode(ErrorCodes.QuestionAnswerInvalid)
            .OverridePropertyName(nameof(GradeQuestionDraftQuery.Answer))
            .When(x => x.Question.Type.HasValue && Enum.IsDefined(x.Question.Type.Value) && x.Question.Type != QuestionType.DragDrop);
        RuleFor(x => x)
            .Must(x => QuestionSchemaReader.Read<EssayAnswer>(x.Answer).Text!.Length <= options.QuestionEssayAnswerMaxLength)
            .WithErrorCode(ErrorCodes.QuestionEssayAnswerTooLong)
            .OverridePropertyName(nameof(GradeQuestionDraftQuery.Answer))
            .When(x => x.Question.Type == QuestionType.Essay && QuestionAnswerRules.CanRead(QuestionType.Essay, x.Answer));
    }
}
