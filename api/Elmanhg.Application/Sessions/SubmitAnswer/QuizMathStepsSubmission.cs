using Core.DDD.Repositories;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public static class QuizMathStepsSubmission
{
    public static async Task SubmitAsync(Session session, SessionItem item, string answer, MathAnswerVerdict? verdict, int? reportedTimeTakenMilliseconds, IQuestionRepository questionRepository, IMathStepGradeRepository mathStepGradeRepository, CancellationToken cancellationToken)
    {
        var submission = session.SubmitForAiGrading(item, answer, reportedTimeTakenMilliseconds);
        if (!submission.IsNew)
        {
            return;
        }

        var question = await questionRepository.GetRequiredAsync(x => x.Id == item.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, include: query => query.IgnoreQueryFilters(), asNoTracking: true).ConfigureAwait(false);
        await mathStepGradeRepository.AddAsync(MathStepGrade.Request(session.StudentId, session.Id, question.SubjectId, item.QuestionId, item.QuestionVersion, item.MaxScore, answer, verdict, submission.SubmittedAt, submission.TimeTakenMilliseconds), cancellationToken).ConfigureAwait(false);
    }
}
