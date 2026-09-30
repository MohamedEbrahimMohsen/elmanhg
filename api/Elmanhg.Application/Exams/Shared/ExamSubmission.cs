using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamSubmission
{
    // Exam questions share one page, so an exam essay has no observable writing time (like every exam attempt).
    private const int ExamEssayTimeTakenMilliseconds = 0;

    public static async Task SubmitAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, IEssayGradeRepository essayGradeRepository, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (session.IsSubmitted)
        {
            return;
        }

        var written = session.Items
            .Where(x => x.SavedAnswer is not null)
            .Select(x => (Item: x, Text: WrittenEssayText(FindRevision(revisions, x), x.SavedAnswer!)))
            .Where(x => x.Text is not null)
            .ToList();
        var essayIds = written
            .Select(x => x.Item.QuestionId)
            .ToHashSet();
        var grades = session.Items
            .Where(x => x.SavedAnswer is not null && !essayIds.Contains(x.QuestionId))
            .ToDictionary(x => x.QuestionId, x => Grade(FindRevision(revisions, x), x.SavedAnswer ?? string.Empty));
        var attempts = session.SubmitExam(grades, essayIds, now);
        if (written.Count > 0)
        {
            var questions = await questionRepository.FindAsync(x => essayIds.Contains(x.Id), cancellationToken, include: query => query.IgnoreQueryFilters(), asNoTracking: true).ConfigureAwait(false);
            var requested = written
                .Select(x => EssayGrade.Request(session.StudentId, session.Id, questions.First(question => question.Id == x.Item.QuestionId).SubjectId, x.Item.QuestionId, x.Item.QuestionVersion, x.Item.MaxScore, x.Text!, session.SubmittedAt!.Value, ExamEssayTimeTakenMilliseconds))
                .ToList();
            await essayGradeRepository.AddRangeAsync(requested, cancellationToken).ConfigureAwait(false);
        }

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

    private static string? WrittenEssayText(QuestionRevision revision, string answer)
    {
        using var document = JsonDocument.Parse(answer);
        return QuestionAnswerRules.TryReadWrittenEssay(revision.ReadSnapshot().Type, document.RootElement, out var text) ? text : null;
    }

    private static QuestionGrade Grade(QuestionRevision revision, string answer)
    {
        using var document = JsonDocument.Parse(answer);
        return revision.Grade(document.RootElement);
    }
}
