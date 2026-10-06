using Core.Localization;
using Core.Storage;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using System.Text.Json;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamSessionResultGenerator
{
    public static ExamSessionResult Generate(Session session, IReadOnlyCollection<QuestionRevision> revisions, Guid? subjectId, string? subjectName, List<ExamUnitResult> units, List<ExamLessonResult> lessons, List<ExamUnitBreakdownResult> unitBreakdown, List<ExamObjectiveResult> weakestObjectives, DateTimeOffset now, ILocalizer localizer, IFileStorage fileStorage)
    {
        var items = session.Items
            .OrderBy(x => x.Position)
            .Select(item => GenerateItem(session, item, FindRevision(revisions, item), localizer, fileStorage))
            .ToList();
        var elapsedMilliseconds = Math.Max(0, (long)((session.SubmittedAt ?? now) - session.StartedAt).TotalMilliseconds);
        bool? isPassed = session.ScorePercent is null ? null : session.ScorePercent >= session.PassMark;
        return new ExamSessionResult(session.Id, session.Kind.ToString(), session.IsTestMode, subjectId, subjectName, units, session.StartedAt, session.TimeLimitMinutes, session.Deadline, now, session.PassMark.GetValueOrDefault(), session.SubmittedAt, session.ScorePercent, isPassed, elapsedMilliseconds, items, lessons, unitBreakdown, weakestObjectives);
    }

    public static ExamItemResult GenerateItem(Session session, SessionItem item, QuestionRevision revision, ILocalizer localizer, IFileStorage fileStorage)
    {
        var shown = SessionResultGenerator.GenerateItem(session, item, revision, localizer, fileStorage);
        return new ExamItemResult(shown.Position, shown.QuestionId, shown.QuestionVersion, shown.Type, shown.Stem, shown.Body, shown.MaxScore, ParseSavedAnswer(item.SavedAnswer), item.AnswerSavedAt, shown.Attempt, shown.CorrectAnswer, shown.Explanation);
    }

    private static QuestionRevision FindRevision(IReadOnlyCollection<QuestionRevision> revisions, SessionItem item)
    {
        // Revisions are never deleted and every foreign key restricts, so a served version always resolves.
        return revisions.FirstOrDefault(x => x.QuestionId == item.QuestionId && x.Version == item.QuestionVersion) ?? throw new InvalidOperationException("Served question revision is missing.");
    }

    private static JsonElement? ParseSavedAnswer(string? savedAnswer)
    {
        if (savedAnswer is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(savedAnswer);
        return document.RootElement.Clone();
    }
}
