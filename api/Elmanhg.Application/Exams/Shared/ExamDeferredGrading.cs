using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamDeferredGrading
{
    // Exam questions share one page, so a deferred exam answer has no observable writing time.
    private const int ExamTimeTakenMilliseconds = 0;

    public static async Task RequestAsync(Session session, IReadOnlyList<(SessionItem Item, string Text)> essays, IReadOnlyList<(SessionItem Item, MathAnswerVerdict? Verdict)> mathSteps, IQuestionRepository questionRepository, IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, CancellationToken cancellationToken)
    {
        if (essays.Count == 0 && mathSteps.Count == 0)
        {
            return;
        }

        var questionIds = essays
            .Select(x => x.Item.QuestionId)
            .Concat(mathSteps.Select(x => x.Item.QuestionId))
            .ToHashSet();
        var questions = await questionRepository.FindAsync(x => questionIds.Contains(x.Id), cancellationToken, include: query => query.IgnoreQueryFilters(), asNoTracking: true).ConfigureAwait(false);
        var submittedAt = session.SubmittedAt!.Value;
        if (essays.Count > 0)
        {
            var requested = essays
                .Select(x => EssayGrade.Request(session.StudentId, session.Id, SubjectOf(questions, x.Item), x.Item.QuestionId, x.Item.QuestionVersion, x.Item.MaxScore, x.Text, submittedAt, ExamTimeTakenMilliseconds))
                .ToList();
            await essayGradeRepository.AddRangeAsync(requested, cancellationToken).ConfigureAwait(false);
        }

        if (mathSteps.Count > 0)
        {
            var requested = mathSteps
                .Select(x => MathStepGrade.Request(session.StudentId, session.Id, SubjectOf(questions, x.Item), x.Item.QuestionId, x.Item.QuestionVersion, x.Item.MaxScore, x.Item.SavedAnswer!, x.Verdict, submittedAt, ExamTimeTakenMilliseconds))
                .ToList();
            await mathStepGradeRepository.AddRangeAsync(requested, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Guid SubjectOf(IEnumerable<Question> questions, SessionItem item) => questions.First(question => question.Id == item.QuestionId).SubjectId;
}
