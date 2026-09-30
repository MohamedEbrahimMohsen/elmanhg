using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Questions;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public static class QuestionResultGenerator
{
    public static QuestionDetailResult GenerateDetail(Question question, IFileStorage fileStorage)
    {
        return new QuestionDetailResult(question.Id, question.LessonId, question.SubjectId, question.Type.ToString(), question.Stem, QuestionBodyMedia.Resolve(question.Type, question.Body, fileStorage), ParseJson(question.GradingSpec), question.Explanation, question.Difficulty.ToString(), question.ObjectiveId, question.Tags.ToList(), question.MaxScore, question.Version, question.ValidationStatus.ToString(), question.RejectionReason, question.RetiredAt);
    }

    public static QuestionListItemResult GenerateListItem(Question question, string lessonName, string? teacherName, bool isServable)
    {
        return new QuestionListItemResult(question.Id, question.LessonId, lessonName, question.SubjectId, question.Type.ToString(), question.Stem, question.Difficulty.ToString(), question.Version, question.ValidationStatus.ToString(), question.ValidatedBy, teacherName, question.RejectionReason, question.UpdationDate, question.RetiredAt, isServable);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
