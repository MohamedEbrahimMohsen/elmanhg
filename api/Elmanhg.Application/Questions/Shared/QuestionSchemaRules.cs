using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionSchemaRules
{
    public static List<string> Validate(QuestionFields fields, ContentOptions options)
    {
        return fields.Type switch
        {
            QuestionType.Mcq => ChoiceQuestionRules.Validate(fields.Body, fields.GradingSpec, multiple: false, options),
            QuestionType.Multi => ChoiceQuestionRules.Validate(fields.Body, fields.GradingSpec, multiple: true, options),
            QuestionType.TrueFalse => TrueFalseQuestionRules.Validate(fields.Body, fields.GradingSpec),
            QuestionType.Fill => FillQuestionRules.Validate(fields.Stem ?? string.Empty, fields.Body, fields.GradingSpec, options),
            QuestionType.Short => ShortQuestionRules.Validate(fields.Body, fields.GradingSpec, options),
            QuestionType.Essay => EssayQuestionRules.Validate(fields.Body, fields.GradingSpec, options),
            QuestionType.MathSteps => MathStepsQuestionRules.Validate(fields.Body, fields.GradingSpec, options),
            QuestionType.DragDrop => DragDropQuestionRules.Validate(fields.Body, fields.GradingSpec, options),
            _ => [],
        };
    }

    public static (string Body, string GradingSpec) Normalize(QuestionFields fields, IRichTextSanitizer sanitizer)
    {
        return fields.Type switch
        {
            QuestionType.Mcq => ChoiceQuestionRules.Normalize(fields.Body, fields.GradingSpec, multiple: false, sanitizer),
            QuestionType.Multi => ChoiceQuestionRules.Normalize(fields.Body, fields.GradingSpec, multiple: true, sanitizer),
            QuestionType.TrueFalse => TrueFalseQuestionRules.Normalize(fields.GradingSpec),
            QuestionType.Fill => FillQuestionRules.Normalize(fields.Body, fields.GradingSpec),
            QuestionType.Short => ShortQuestionRules.Normalize(fields.Body, fields.GradingSpec),
            QuestionType.Essay => EssayQuestionRules.Normalize(fields.Body, fields.GradingSpec, sanitizer),
            QuestionType.MathSteps => MathStepsQuestionRules.Normalize(fields.Body, fields.GradingSpec),
            QuestionType.DragDrop => DragDropQuestionRules.Normalize(fields.Body, fields.GradingSpec),
            _ => throw new InvalidOperationException("Unsupported question type."),
        };
    }
}
