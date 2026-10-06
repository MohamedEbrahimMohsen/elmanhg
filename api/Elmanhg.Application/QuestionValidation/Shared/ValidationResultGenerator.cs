using Core.Storage;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using System.Text.Json;

namespace Elmanhg.Application.QuestionValidation.Shared;

public static class ValidationResultGenerator
{
    public static ValidationQueueItemResult GenerateQueueItem(Question question, Lesson? lesson, CurriculumUnit? unit, bool openedInSession)
    {
        return new ValidationQueueItemResult(question.Id, question.SubjectId, lesson?.UnitId ?? Guid.Empty, unit?.Name ?? string.Empty, question.LessonId, lesson?.Name ?? string.Empty, question.Type.ToString(), question.Stem, question.Difficulty.ToString(), question.Version, question.SubmittedAt, openedInSession);
    }

    public static ValidationQuestionDetailResult GenerateDetail(Question question, Subject? subject, CurriculumUnit? unit, Lesson lesson, Dictionary<Guid, string> deciderNames, IFileStorage fileStorage)
    {
        var objectiveText = lesson.Objectives.FirstOrDefault(x => x.Id == question.ObjectiveId)?.Text;
        return new ValidationQuestionDetailResult(question.Id, question.SubjectId, subject?.Name ?? string.Empty, lesson.UnitId, unit?.Name ?? string.Empty, lesson.Id, lesson.Name, lesson.State.ToString(), question.Type.ToString(), question.Stem, QuestionBodyMedia.Resolve(question.Type, question.Body, fileStorage), ParseJson(question.GradingSpec), question.Explanation, question.Difficulty.ToString(), question.ObjectiveId, objectiveText, question.Tags.ToList(), question.MaxScore, question.Version, question.ValidationStatus.ToString(), question.RejectionReason, question.SubmittedAt, question.RetiredAt, GenerateRevisions(question), GenerateDecisions(question, deciderNames));
    }

    private static List<QuestionRevisionEntryResult> GenerateRevisions(Question question)
    {
        return question.Revisions
            .OrderBy(x => x.Version)
            .Select(x => new QuestionRevisionEntryResult(x.Version, x.EditedAt))
            .ToList();
    }

    private static List<QuestionDecisionResult> GenerateDecisions(Question question, Dictionary<Guid, string> deciderNames)
    {
        return question.Decisions
            .OrderBy(x => x.DecidedAt)
            .Select(x => new QuestionDecisionResult(x.Version, x.Outcome.ToString(), x.Reason, x.Difficulty.ToString(), x.DifficultyChangedFrom?.ToString(), x.DecidedBy, deciderNames.GetValueOrDefault(x.DecidedBy), x.DecidedAt))
            .ToList();
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
