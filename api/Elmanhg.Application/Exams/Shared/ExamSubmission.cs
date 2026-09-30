using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using System.Text.Json;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamSubmission
{
    public static async Task SubmitAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, IAiMathCheckClient mathCheckClient, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken)
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
        Dictionary<Guid, QuestionGrade> grades = [];
        List<(SessionItem Item, MathAnswerVerdict? Verdict)> mathSteps = [];
        foreach (var item in session.Items.Where(x => x.SavedAnswer is not null && !essayIds.Contains(x.QuestionId)))
        {
            var decision = await DecideAsync(FindRevision(revisions, item), item.SavedAnswer!, mathCheckClient, cancellationToken).ConfigureAwait(false);
            if (decision.Grade is null)
            {
                mathSteps.Add((item, decision.Verdict));
            }
            else
            {
                grades[item.QuestionId] = decision.Grade;
            }
        }

        var deferredIds = essayIds
            .Concat(mathSteps.Select(x => x.Item.QuestionId))
            .ToHashSet();
        var attempts = session.SubmitExam(grades, deferredIds, now)
            .Where(x => !grades[x.QuestionId].AwaitsReview)
            .ToList();
        await ExamDeferredGrading.RequestAsync(session, written.Select(x => (x.Item, x.Text!)).ToList(), mathSteps, questionRepository, essayGradeRepository, mathStepGradeRepository, cancellationToken).ConfigureAwait(false);

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

    private static async Task<AnswerDecision> DecideAsync(QuestionRevision revision, string answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(answer);
        return await AnswerGrader.DecideAsync(revision, document.RootElement, mathCheckClient, cancellationToken).ConfigureAwait(false);
    }
}
