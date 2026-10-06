using Core.DDD.Repositories;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Sessions.SubmitAnswer;

public static class QuizEssaySubmission
{
    public static async Task SubmitAsync(Session session, SessionItem item, string essayText, int? reportedTimeTakenMilliseconds, IQuestionRepository questionRepository, IEssayGradeRepository essayGradeRepository, CancellationToken cancellationToken)
    {
        var submission = session.SubmitEssay(item, QuestionSchemaReader.Serialize(new EssayAnswer(essayText)), reportedTimeTakenMilliseconds);
        if (!submission.IsNew)
        {
            return;
        }

        var question = await questionRepository.GetRequiredAsync(x => x.Id == item.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, include: query => query.IgnoreQueryFilters(), asNoTracking: true).ConfigureAwait(false);
        await essayGradeRepository.AddAsync(EssayGrade.Request(session.StudentId, session.Id, question.SubjectId, item.QuestionId, item.QuestionVersion, item.MaxScore, essayText, submission.SubmittedAt, submission.TimeTakenMilliseconds), cancellationToken).ConfigureAwait(false);
    }
}
