using Elmanhg.Domain.Questions;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionResultGenerator
{
    public static QuestionDetailResult GenerateDetail(Question question)
    {
        return new QuestionDetailResult(question.Id, question.LessonId, question.SubjectId, question.Type.ToString(), question.Stem, ParseJson(question.Body), ParseJson(question.GradingSpec), question.Explanation, question.Difficulty.ToString(), question.ObjectiveId, question.Tags.ToList(), question.MaxScore, question.Version, question.ValidationStatus.ToString());
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
