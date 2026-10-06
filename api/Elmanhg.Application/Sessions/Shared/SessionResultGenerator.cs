using Core.Localization;
using Core.Storage;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Application.Sessions.Shared;

public static class SessionResultGenerator
{
    public static SessionResult Generate(Session session, IReadOnlyCollection<QuestionRevision> revisions, ILocalizer localizer, IFileStorage fileStorage)
    {
        var items = session.Items
            .OrderBy(x => x.Position)
            .Select(item => GenerateItem(session, item, FindRevision(revisions, item), localizer, fileStorage))
            .ToList();
        return new SessionResult(session.Id, session.Kind.ToString(), Parse(session.Scope), session.IsTestMode, session.StartedAt, session.SubmittedAt, session.ScorePercent, session.TotalTimeTakenMilliseconds, session.CurrentPosition, items);
    }

    public static SessionItemResult GenerateItem(Session session, SessionItem item, QuestionRevision revision, ILocalizer localizer, IFileStorage fileStorage)
    {
        var snapshot = revision.ReadSnapshot();
        var attempt = session.FindAttempt(item.QuestionId);
        var pendingAnswer = session.FindPendingEssayAnswer(item);
        var reveal = attempt is not null || pendingAnswer is not null || session.IsSubmitted;
        var attemptResult = attempt is null ? null : new AttemptResult(attempt.Id, Parse(attempt.Answer), attempt.Score, attempt.NormalisedScore, attempt.Outcome.ToString(), attempt.AwaitsReview, GradeFeedbackText.Localize(attempt.ReadFeedback(), localizer), attempt.TimeTakenMilliseconds, attempt.CreatedAt);
        return new SessionItemResult(item.Position, item.QuestionId, item.QuestionVersion, snapshot.Type.ToString(), snapshot.Stem, QuestionBodyMedia.Resolve(snapshot.Type, snapshot.Body?.ToJsonString() ?? "{}", fileStorage), item.MaxScore, attemptResult, reveal ? ToElement(snapshot.GradingSpec) : null, reveal ? snapshot.Explanation : null, pendingAnswer is null ? null : Parse(pendingAnswer));
    }

    private static QuestionRevision FindRevision(IReadOnlyCollection<QuestionRevision> revisions, SessionItem item)
    {
        // Revisions are never deleted and every foreign key restricts, so a served version always resolves.
        return revisions.FirstOrDefault(x => x.QuestionId == item.QuestionId && x.Version == item.QuestionVersion) ?? throw new InvalidOperationException("Served question revision is missing.");
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement ToElement(JsonNode? node) => Parse(node?.ToJsonString() ?? "{}");
}
