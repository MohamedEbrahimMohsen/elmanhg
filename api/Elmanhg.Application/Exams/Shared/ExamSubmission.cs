using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using System.Text.Json;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamSubmission
{
    public static async Task SubmitAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionMasteryRepository questionMasteryRepository, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (session.IsSubmitted)
        {
            return;
        }

        var grades = session.Items
            .Where(x => x.SavedAnswer is not null)
            .ToDictionary(x => x.QuestionId, x => Grade(FindRevision(revisions, x), x.SavedAnswer ?? string.Empty));
        var attempts = session.SubmitExam(grades, now);
        if (session.IsTestMode || attempts.Count == 0)
        {
            return;
        }

        var studentId = session.StudentId;
        var questionIds = attempts
            .Select(x => x.QuestionId)
            .ToList();
        var masteries = await questionMasteryRepository.FindAsync(x => x.StudentId == studentId && questionIds.Contains(x.QuestionId), cancellationToken).ConfigureAwait(false);
        List<QuestionMastery> started = [];
        foreach (var attempt in attempts)
        {
            var masteryAttempt = MasteryAttempt.From(attempt);
            var mastery = masteries.FirstOrDefault(x => x.QuestionId == attempt.QuestionId);
            if (mastery is null)
            {
                started.Add(QuestionMastery.Start(studentId, attempt.QuestionId, masteryAttempt));
            }
            else
            {
                mastery.Record(masteryAttempt, correctThreshold);
            }
        }

        if (started.Count > 0)
        {
            await questionMasteryRepository.AddRangeAsync(started, cancellationToken).ConfigureAwait(false);
        }
    }

    private static QuestionRevision FindRevision(IReadOnlyCollection<QuestionRevision> revisions, SessionItem item)
    {
        return revisions.FirstOrDefault(x => x.QuestionId == item.QuestionId && x.Version == item.QuestionVersion) ?? throw new InvalidOperationException("Served question revision is missing.");
    }

    private static QuestionGrade Grade(QuestionRevision revision, string answer)
    {
        using var document = JsonDocument.Parse(answer);
        return revision.Grade(document.RootElement);
    }
}
