using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.Questions.Grading;

public static class QuestionGrader
{
    public static QuestionGrade Grade(QuestionType type, string gradingSpec, int maxScore, JsonElement answer)
    {
        var grade = type switch
        {
            QuestionType.Mcq => ChoiceGrader.GradeMcq(ReadSpec<McqGradingSpec>(gradingSpec), ReadAnswer<McqAnswer>(answer)),
            QuestionType.Multi => ChoiceGrader.GradeMulti(ReadSpec<MultiGradingSpec>(gradingSpec), ReadAnswer<MultiAnswer>(answer)),
            QuestionType.TrueFalse => ChoiceGrader.GradeTrueFalse(ReadSpec<TrueFalseGradingSpec>(gradingSpec), ReadAnswer<TrueFalseAnswer>(answer)),
            QuestionType.Fill => new NormalisedGrade(TextGrader.GradeFill(ReadSpec<FillGradingSpec>(gradingSpec), ReadAnswer<FillAnswer>(answer)), null),
            QuestionType.Short => new NormalisedGrade(TextGrader.GradeShort(ReadSpec<ShortGradingSpec>(gradingSpec), ReadAnswer<ShortAnswer>(answer)), null),
            _ => throw new InvalidOperationException("Unsupported question type."),
        };
        return QuestionGrade.FromNormalised(grade, maxScore);
    }

    private static T ReadSpec<T>(string json) where T : class
    {
        var value = JsonSerializer.Deserialize<T>(json, QuestionJson.SerializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Question grading spec is not readable.");
        }

        return value;
    }

    private static T ReadAnswer<T>(JsonElement answer) where T : class
    {
        var value = answer.Deserialize<T>(QuestionJson.SerializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Question answer was not validated.");
        }

        return value;
    }
}
