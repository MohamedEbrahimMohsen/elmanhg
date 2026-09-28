using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class TrueFalseQuestionRules
{
    public static List<string> Validate(JsonElement body, JsonElement gradingSpec)
    {
        List<string> errors = [];
        QuestionSchemaReader.AddIf(errors, !QuestionSchemaReader.TryRead<TrueFalseBody>(body, out _), ErrorCodes.QuestionBodyInvalid);
        if (!QuestionSchemaReader.TryRead<TrueFalseGradingSpec>(gradingSpec, out var spec))
        {
            errors.Add(ErrorCodes.QuestionGradingSpecInvalid);
            return errors;
        }

        QuestionSchemaReader.AddIf(errors, spec.CorrectAnswer is null, ErrorCodes.QuestionCorrectAnswerRequired);
        return errors;
    }

    public static (string Body, string GradingSpec) Normalize(JsonElement gradingSpec)
    {
        var spec = QuestionSchemaReader.Read<TrueFalseGradingSpec>(gradingSpec);
        return (QuestionSchemaReader.Serialize(new TrueFalseBody()), QuestionSchemaReader.Serialize(new TrueFalseGradingSpec(spec.CorrectAnswer)));
    }
}
